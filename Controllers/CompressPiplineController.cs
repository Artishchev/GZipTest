using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace GZipTest.Controllers
{
    internal class CompressPiplineController : PiplineController
    {
        public override async Task<bool> PerformAction(string inputFile, string outputFile, bool compress, CancellationToken cancellationToken = default(CancellationToken))
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
    }
}
