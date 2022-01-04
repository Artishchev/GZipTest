using GZipTest.Controllers;
using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Linq;

namespace GZipTest.Controllers
{
    /// <summary>
    /// Controller for DataFlow Pipelines
    /// Initializes the processing pipeline
    /// </summary>
    sealed class PiplineController
    {
        static ICompressionController compressionController = new GZipController();
        static IDataReader dataReader;

        /// <summary>
        /// Perform compression and decompression pipelines
        /// </summary>
        /// <param name="inputFile">Original filename. File to be compressed or decompressed depending on <paramref name="compress"/> flag</param>
        /// <param name="outputFile">Destination filename</param>
        /// <param name="compress">The flag defines the operation to be performed (compression if true / decompression if false)</param>
        /// <param name="cancellationToken">In case of errors or termination by user</param>
        /// <returns>True on sucess. False on errors</returns>
        public async Task<bool> PerformAction(string inputFile, string outputFile, bool compress, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (compress)
                return await CompressPipeline(inputFile, outputFile, compress, cancellationToken);
            else
                return await DecompressPipeline(inputFile, outputFile, compress, cancellationToken);
        }

        private async Task<bool> DecompressPipeline(string inputFile, string outputFile, bool compress, CancellationToken cancellationToken = default(CancellationToken))
        {
            ConcurrentBag<long> headers = new ConcurrentBag<long>();
            ConcurrentBag<long> possibleHeaders = new ConcurrentBag<long>();

            var findHeaders = new TransformBlock<DataChunk, (long[], long)>((DataChunk chunk) => {
                var result = CompressedFileReader.FindChunks(chunk);
                long[] resultHeaders = null;
                long resultPossibleHeader = -1;
                if (result.Item1.Length > 0)
                {
                    foreach (var headerPos in result.Item1)
                    {
                        headers.Add(headerPos);
                    }
                    resultHeaders = result.Item1;
                }
                if (result.Item2 >= 0)
                {
                    possibleHeaders.Add(result.Item2);
                    resultPossibleHeader = result.Item2;
                }
                return (resultHeaders, resultPossibleHeader);

            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount, CancellationToken = cancellationToken });

            var agregateHeadersBlock = new BatchBlock<(long[], long)>(int.MaxValue);

            var sortHeaders = new TransformManyBlock<IEnumerable<(long[], long)>, DataChunk>(async (IEnumerable<(long[], long)> input) => {
                List<long> headers = new List<long>();
                List<long> possibleHeaders = new List<long>();
                foreach (var item in input)
                {
                    if (item.Item1?.Length > 0)
                    {
                        headers.AddRange(item.Item1);
                    }
                    if (item.Item2 >= 0)
                    {
                        possibleHeaders.Add(item.Item2);
                    }
                }

                headers.AddRange(await CompressedFileReader.CheckPossibleHeaders(inputFile, possibleHeaders.ToArray()));
                headers = headers.Distinct().OrderBy(h => h).ToList();

                DataChunk[] result = new DataChunk[headers.Count];

                for (int i = 0; i < headers.Count - 1; i++)
                {
                    result[i] = new DataChunk(){ chunksCount = headers.Count, orderNum = i, offset = (int)headers[i], length = (int)(headers[i + 1] - headers[i]) };
                }
                //Adding last chunk
                result[result.Length - 1] = new DataChunk(){ chunksCount = headers.Count, orderNum = result.Length - 1, offset = (int)headers[result.Length - 1], length = (int)(new FileInfo(inputFile).Length - headers[result.Length - 1]) };

                return result;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken });

            var decompressToTemp = new TransformBlock<DataChunk, DataChunk>(async (DataChunk dataChunk) => {
                await new GZipController().DecompressToFileAsync(inputFile, outputFile, dataChunk);
                return dataChunk;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount, CancellationToken = cancellationToken });

            var agregateDecompressedChunksBlock = new BatchBlock<DataChunk>(int.MaxValue);

            var writeResultFile = new ActionBlock<DataChunk[]>(async (DataChunk[] dataChunks) => {
                using (FileStream output = File.Open(outputFile, FileMode.Append))
                {
                    foreach (var chnk in dataChunks)
                    {
                        using (FileStream input = File.OpenRead(chnk.chunkFileName))
                        {
                            await input.CopyToAsync(output);
                        }
                        File.Delete(chnk.chunkFileName);
                    }
                }
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, BoundedCapacity = 1, CancellationToken = cancellationToken });

            DataflowLinkOptions linkOption = new DataflowLinkOptions() { PropagateCompletion = true };

            findHeaders.LinkTo(agregateHeadersBlock, linkOption);
            agregateHeadersBlock.LinkTo(sortHeaders, linkOption);
            sortHeaders.LinkTo(decompressToTemp, linkOption);
            decompressToTemp.LinkTo(agregateDecompressedChunksBlock, linkOption);
            agregateDecompressedChunksBlock.LinkTo(writeResultFile, linkOption);

            dataReader = new UncompressedFileReader();

            try
            {
                await CompressedFileReader.CheckFileFormatAsync(inputFile, cancellationToken);

                await foreach (DataChunk dataChunk in dataReader.Read(inputFile).WithCancellation(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await findHeaders.SendAsync(dataChunk);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new DetailedMessageException("Error reading input file. The program does not have the required permission or file is in use.", ex);
            }

            findHeaders.Complete();

            await writeResultFile.Completion;

            return false;
        }

        private async Task<bool> CompressPipeline(string inputFile, string outputFile, bool compress, CancellationToken cancellationToken = default(CancellationToken))
        {
            var transform = new TransformBlock<DataChunk, DataChunk>((DataChunk chunk) => {
                return CompressOrDecompress(chunk, compress);
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount, CancellationToken = cancellationToken });

            var write = new TransformBlock<DataChunk, DataChunk>(async (DataChunk chunk) => {
                return await Write(chunk, outputFile, compress);
            }, new ExecutionDataflowBlockOptions() { CancellationToken = cancellationToken });

            var progressBar = new ActionBlock<DataChunk>((DataChunk chunk) =>
            {
                ProgressBar(chunk.orderNum + 1, chunk.chunksCount);
            }, new ExecutionDataflowBlockOptions() { CancellationToken = cancellationToken });

            DataflowLinkOptions linkOption = new DataflowLinkOptions() { PropagateCompletion = true };
            transform.LinkTo(write, linkOption);
            write.LinkTo(progressBar, linkOption);

            dataReader = compress ? new UncompressedFileReader() : new CompressedFileReader();

            try
            {
                await foreach (DataChunk dataChunk in dataReader.Read(inputFile).WithCancellation(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await transform.SendAsync(dataChunk);
                }

            }
            catch (UnauthorizedAccessException ex)
            {
                throw new DetailedMessageException("Error reading input file. The program does not have the required permission or file is in use.", ex);
            }

            transform.Complete();

            await progressBar.Completion;

            return true;
        }

        /// <summary>
        /// Perform compression or decompression
        /// </summary>
        /// <param name="chunk">Data chunk to process</param>
        /// <param name="compress">The flag defines the operation to be performed (compression if true / decompression if false)</param>
        /// <returns>Processed DataChunk</returns>
        private DataChunk CompressOrDecompress(DataChunk chunk, bool compress)
        {
            MemoryOwner<byte> buffer;
            if (compress)
            {
                buffer = compressionController.Compress(chunk.uncompressedData);
                chunk.uncompressedData.Dispose();
                chunk.compressedData = buffer;
            }
            else
            {
                buffer = compressionController.Decompress(chunk.compressedData);
                chunk.compressedData.Dispose();
                chunk.uncompressedData = buffer;
            }

            return chunk;
        }

        /// <summary>
        /// Writes data chunk to the output file
        /// </summary>
        /// <param name="chunk">Data chunk to write</param>
        /// <param name="outputFile">Destination filename</param>
        /// <param name="compress"></param>
        /// <returns>Written data chunk without buffers</returns>
        private async Task<DataChunk> Write(DataChunk chunk, string outputFile, bool compress)
        {
            long outputLength = 0;
            try
            {
                using Stream stream = File.Open(outputFile, FileMode.Append);
                {
                    MemoryOwner<byte> memoryOwner = compress ? chunk.compressedData : chunk.uncompressedData;
                    await stream.WriteAsync(memoryOwner.Memory);
                    outputLength = memoryOwner.Length;
                    memoryOwner.Dispose();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new DetailedMessageException("Error writing to output file. The program does not have the required permission or file is in use.", ex);
            }
            catch (Exception ex)
            {
                throw new DetailedMessageException("Error writing to output file. Please check the output path.", ex);
            }

            return chunk;
        }

        /// <summary>
        /// Displays current progress to the console
        /// </summary>
        /// <param name="progress">Current processed item</param>
        /// <param name="tot">Total items</param>
        private static void ProgressBar(int progress, int tot)
        {
            if (tot < progress)
            {
                progress = tot;
            }
            //draw empty progress bar
            Console.CursorVisible = false;
            Console.CursorLeft = 0;
            Console.Write("["); //start
            Console.CursorLeft = 32;
            Console.Write("]"); //end
            Console.CursorLeft = 1;
            float onechunk = 30.0f / tot;

            //draw filled part
            int position = 1;
            for (int i = 0; i <= onechunk * progress; i++)
            {
                Console.CursorLeft = position++;
                Console.Write("+");
            }

            //draw unfilled part
            for (int i = position; i <= 31; i++)
            {
                Console.CursorLeft = position++;
                Console.Write("-");
            }

            //draw totals
            Console.CursorLeft = 35;
            Console.Write(progress.ToString() + " of " + tot.ToString() + "    "); //blanks at the end remove any excess
        }
    }
}
