using GZipTest.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace GZipTest.Controllers
{
    /// <summary>
    /// Implements decompression pipeline
    /// </summary>
    internal class DecompressPiplineController : PiplineController
    {
        /// <summary>
        /// Perform data reading operation from original file
        /// </summary>
        internal static CompressedFileReader dataReader = new CompressedFileReader();

        /// <summary>
        /// Perform decompression pipeline
        /// </summary>
        /// <param name="inputFile">Original filename. File to be decompressed depending on <paramref name="compress"/> flag</param>
        /// <param name="outputFile">Destination filename</param>
        /// <param name="compress">The flag defines the operation to be performed (compression if true / decompression if false)</param>
        /// <param name="cancellationToken">In case of errors or termination by user</param>
        /// <returns>True on sucess. False on errors</returns>
        public override async Task<bool> PerformAction(string inputFile, string outputFile, CancellationToken cancellationToken = default(CancellationToken))
        {
            // searches for headers in file
            var findHeaders = new TransformBlock<DataChunk, (List<long>, long)>((DataChunk chunk) => {
                List<long> resultHeaders = new List<long>();
                long resultPossibleHeader = -1;
                
                var possibleHeader = dataReader.FindHeaders(chunk, resultHeaders);

                if (possibleHeader >= 0)
                {
                    resultPossibleHeader = possibleHeader;
                }
                return (resultHeaders, resultPossibleHeader);

            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount, CancellationToken = cancellationToken });

            // aggregates headers for future processing
            var agregateHeadersBlock = new BatchBlock<(List<long>, long)>(int.MaxValue);

            // checks headers at the borders of chunks and forms a gzipped data chunks
            var checkHeaders = new TransformManyBlock<IEnumerable<(List<long>, long)>, DataChunk>(async (IEnumerable<(List<long>, long)> input) => {
                List<long> headers = new List<long>();
                List<long> possibleHeaders = new List<long>();
                DataChunk[] result = null;
                foreach (var item in input)
                {
                    if (item.Item1?.Count > 0)
                    {
                        headers.AddRange(item.Item1);
                    }
                    if (item.Item2 >= 0)
                    {
                        possibleHeaders.Add(item.Item2);
                    }
                }

                // Checks headers at the border of chunks (possible headers)
                headers.AddRange(await dataReader.CheckPossibleHeaders(inputFile, possibleHeaders.ToArray()));
                headers = headers.Distinct().OrderBy(h => h).ToList();

                if (headers.Count > 0)
                {
                    result = new DataChunk[headers.Count];

                    for (int i = 0; i < headers.Count - 1; i++)
                    {
                        result[i] = new DataChunk() { chunksCount = headers.Count, orderNum = i, offset = headers[i], length = headers[i + 1] - headers[i] };
                    }
                    //Adding last chunk
                    result[result.Length - 1] = new DataChunk() { chunksCount = headers.Count, orderNum = result.Length - 1, offset = headers[result.Length - 1], length = new FileInfo(inputFile).Length - headers[result.Length - 1] };
                }

                return result;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken });

            // decompress gzip chunks to separate files in parallel
            var decompressToTemp = new TransformBlock<DataChunk, DataChunk>(async (DataChunk dataChunk) => {
                await new GZipController().DecompressToTempFilesAsync(inputFile, outputFile, dataChunk);
                return dataChunk;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount, CancellationToken = cancellationToken });

            // show progress to user
            var showProgress = new TransformBlock<DataChunk, DataChunk>((DataChunk dataChunk) => {
                ProgressBar(dataChunk.orderNum + 1, dataChunk.chunksCount + 1);
                return dataChunk;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, BoundedCapacity = 1, CancellationToken = cancellationToken });

            // aggreagte chunks to exclude unordered write to output file
            var agregateDecompressedChunksBlock = new BatchBlock<DataChunk>(int.MaxValue);

            var writeResultFile = new ActionBlock<DataChunk[]>(async (DataChunk[] dataChunks) => {
                dataChunks = dataChunks.OrderBy(dc => dc.orderNum).ToArray();
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
                // Final progress
                ProgressBar(dataChunks[0].chunksCount + 1, dataChunks[0].chunksCount + 1);
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, BoundedCapacity = 1, CancellationToken = cancellationToken });

            DataflowLinkOptions linkOption = new DataflowLinkOptions() { PropagateCompletion = true };

            findHeaders.LinkTo(agregateHeadersBlock, linkOption);
            agregateHeadersBlock.LinkTo(checkHeaders, linkOption);
            checkHeaders.LinkTo(decompressToTemp, linkOption);
            decompressToTemp.LinkTo(showProgress, linkOption);
            showProgress.LinkTo(agregateDecompressedChunksBlock, linkOption);
            agregateDecompressedChunksBlock.LinkTo(writeResultFile, linkOption);

            try
            {
                await dataReader.CheckCompressedFileFormatAsync(inputFile, cancellationToken);

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
    }
}
