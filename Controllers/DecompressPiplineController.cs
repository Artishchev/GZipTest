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
            ConcurrentBag<long> headers = new ConcurrentBag<long>();
            ConcurrentBag<long> possibleHeaders = new ConcurrentBag<long>();

            var findHeaders = new TransformBlock<DataChunk, (long[], long)>((DataChunk chunk) => {
                var result = dataReader.FindHeaders(chunk);
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

                headers.AddRange(await dataReader.CheckPossibleHeaders(inputFile, possibleHeaders.ToArray()));
                headers = headers.Distinct().OrderBy(h => h).ToList();

                DataChunk[] result = new DataChunk[headers.Count];

                for (int i = 0; i < headers.Count - 1; i++)
                {
                    result[i] = new DataChunk() { chunksCount = headers.Count, orderNum = i, offset = (int)headers[i], length = (int)(headers[i + 1] - headers[i]) };
                }
                //Adding last chunk
                result[result.Length - 1] = new DataChunk() { chunksCount = headers.Count, orderNum = result.Length - 1, offset = (int)headers[result.Length - 1], length = (int)(new FileInfo(inputFile).Length - headers[result.Length - 1]) };

                return result;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken });

            var decompressToTemp = new TransformBlock<DataChunk, DataChunk>(async (DataChunk dataChunk) => {
                await new GZipController().DecompressToFileAsync(inputFile, outputFile, dataChunk);
                return dataChunk;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount, CancellationToken = cancellationToken });

            var showProgress = new TransformBlock<DataChunk, DataChunk>((DataChunk dataChunk) => {
                ProgressBar(dataChunk.orderNum + 1, dataChunk.chunksCount + 1);
                return dataChunk;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, BoundedCapacity = 1, CancellationToken = cancellationToken });

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
            agregateHeadersBlock.LinkTo(sortHeaders, linkOption);
            sortHeaders.LinkTo(decompressToTemp, linkOption);
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
