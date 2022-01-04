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
    /// <summary>
    /// Implements compression pipeline
    /// </summary>
    internal class CompressPiplineController : PiplineController
    {
        /// <summary>
        /// Perform data reading operation from original file
        /// </summary>
        internal static FileReader dataReader = new FileReader();

        /// <summary>
        /// Perform compression pipelines
        /// </summary>
        /// <param name="inputFile">Original filename. File to be compressed</param>
        /// <param name="outputFile">Destination filename</param>
        /// <param name="compress">The flag defines the operation to be performed (compression if true / decompression if false)</param>
        /// <param name="cancellationToken">In case of errors or termination by user</param>
        /// <returns>True on sucess. False on errors</returns>
        public override async Task<bool> PerformAction(string inputFile, string outputFile, CancellationToken cancellationToken = default(CancellationToken))
        {
            // compresses data to gzip
            var transform = new TransformBlock<DataChunk, DataChunk>((DataChunk chunk) => {
                MemoryOwner<byte> buffer;
                buffer = compressionController.Compress(chunk.inputData);
                chunk.inputData.Dispose();
                chunk.outputData = buffer;
                return chunk;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount, CancellationToken = cancellationToken });

            // writes data to output file
            var write = new TransformBlock<DataChunk, DataChunk>(async (DataChunk chunk) => {
                return await Write(chunk, outputFile);
            }, new ExecutionDataflowBlockOptions() { CancellationToken = cancellationToken });

            // shows a progress to a user
            var progressBar = new ActionBlock<DataChunk>((DataChunk chunk) =>
            {
                ProgressBar(chunk.orderNum + 1, chunk.chunksCount);
            }, new ExecutionDataflowBlockOptions() { CancellationToken = cancellationToken });

            DataflowLinkOptions linkOption = new DataflowLinkOptions() { PropagateCompletion = true };
            transform.LinkTo(write, linkOption);
            write.LinkTo(progressBar, linkOption);

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
        /// Writes data chunk to the output file
        /// </summary>
        /// <param name="chunk">Data chunk to write</param>
        /// <param name="outputFile">Destination filename</param>
        /// <returns>Written data chunk without buffers</returns>
        private async Task<DataChunk> Write(DataChunk chunk, string outputFile)
        {
            long outputLength = 0;
            try
            {
                using Stream stream = File.Open(outputFile, FileMode.Append);
                {
                    MemoryOwner<byte> memoryOwner = chunk.outputData;
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
