using GZipTest.Controllers;
using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace GZipTest
{
    sealed class GZipTest
    {
        static ICompressionController compressionController = new GZipController();
        static IDataReader dataReader;

        public async Task<bool> PerformAction(string inputFile, string outputFile, bool compress)
        {
            int chunkLength = 1000000;
            ExecutionDataflowBlockOptions execOptions = new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, BoundedCapacity = Environment.ProcessorCount };

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
            }, execOptions);

            var write = new ActionBlock<DataChunk>((DataChunk chunk) => {
                long outputLength = 0;
                using Stream stream = File.Open(outputFile, FileMode.Append);
                {
                    MemoryOwner<byte> memoryOwner = compress ? chunk.compressedData : chunk.uncompressedData;
                    stream.Write(memoryOwner.Span);
                    outputLength = memoryOwner.Length;
                    memoryOwner.Dispose();
                }
                Console.WriteLine($"Action {chunk.orderNum} Length:{outputLength}");
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1 });

            DataflowLinkOptions linkOption = new DataflowLinkOptions() { PropagateCompletion = true };
            transform.LinkTo(write, linkOption);

            dataReader = compress ? new UncompressedFileReader() : new CompressedFileReader();
            await foreach (DataChunk dataChunk in dataReader.Read(inputFile))
                await transform.SendAsync(dataChunk);

            //long fileLength = new FileInfo(inputFile).Length;
            //MemoryOwner<byte> buffer;
            //using (Stream stream = File.OpenRead(inputFile))
            //{
            //    for (int i = 0; i < fileLength / chunkLength; i++)
            //    {
            //        buffer = MemoryOwner<byte>.Allocate(chunkLength);
            //        stream.Read(buffer.Span);
            //        await transform.SendAsync((buffer, i));
            //        Console.WriteLine($"Send {i}");
            //    }
            //    if (stream.Length - stream.Position > 0)
            //    {
            //        int i = -1;
            //        buffer = MemoryOwner<byte>.Allocate((int)(stream.Length - stream.Position));
            //        stream.Read(buffer.Span);
            //        await transform.SendAsync((buffer, i));
            //        Console.WriteLine($"Send {i}");
            //    }
            //}

            transform.Complete();

            await write.Completion;

            return true;
        }
    }
}
