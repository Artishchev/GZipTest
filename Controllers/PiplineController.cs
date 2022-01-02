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

            var findHeaders = new ActionBlock<DataChunk>((DataChunk chunk) => {
                var result = CompressedFileReader.FindChunks(chunk);
                if (result.Item1.Length > 0)
                {
                    foreach (var headerPos in result.Item1)
                    {
                        headers.Add(headerPos);
                    }
                }
                else if (result.Item2 >= 0)
                {
                    possibleHeaders.Add(result.Item2);
                }
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount, CancellationToken = cancellationToken });

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

            await findHeaders.Completion;

            long[] h = headers.ToArray();
            List<DataChunk> dataChunks = new List<DataChunk>();
            Array.Sort(h);
            GZipController gzc = new GZipController();
            for (int i = 0; i < h.Length; i++)
            {
                int offset, length;
                if (i == h.Length - 1)
                {
                    long fileLength = new FileInfo(inputFile).Length;
                    offset = (int)h[i];
                    length = (int)(fileLength - h[i]);
                }
                else
                {
                    offset = (int)h[i];
                    length = (int)(h[i + 1] - h[i]);
                }
                dataChunks.Add(new DataChunk() { chunksCount = h.Length, orderNum = i, offset = offset, length = length });
            }

            foreach (var chnk in dataChunks)
            {
                await gzc.DecompressToFileAsync(inputFile, outputFile, chnk);
            }

            using (FileStream output = File.Open(outputFile, FileMode.Append))
            {
                foreach (var chnk in dataChunks)
                {
                    using(FileStream input = File.OpenRead(chnk.chunkFileName))
                    {
                        input.CopyTo(output);
                    }
                }

            }

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
