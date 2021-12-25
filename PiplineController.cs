using GZipTest.Controllers;
using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace GZipTest
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
            var transform = new TransformBlock<DataChunk, DataChunk>((DataChunk chunk) => {
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

                Console.WriteLine($"Transform {chunk.orderNum} Length:{buffer.Length}");

                return chunk;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount, CancellationToken = cancellationToken });

            var write = new ActionBlock<DataChunk>((DataChunk chunk) => {
                long outputLength = 0;
                try
                {
                    using Stream stream = File.Open(outputFile, FileMode.Append);
                    {
                        MemoryOwner<byte> memoryOwner = compress ? chunk.compressedData : chunk.uncompressedData;
                        stream.Write(memoryOwner.Span);
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
                
                Console.WriteLine($"Action {chunk.orderNum} Length:{outputLength}");
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken });

            DataflowLinkOptions linkOption = new DataflowLinkOptions() { PropagateCompletion = true };
            transform.LinkTo(write, linkOption);

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

            await write.Completion;

            return true;
        }
    }
}
