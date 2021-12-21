using GZipTest.Controllers;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
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

        public async Task<bool> PerformAction(string inputFile, string outputFile, bool compress)
        {
            int chunkLength = 100000000;
            ExecutionDataflowBlockOptions execOptions = new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1 /*Environment.ProcessorCount*/, BoundedCapacity = Environment.ProcessorCount };

            var transform = new TransformBlock<(MemoryOwner<byte>, int), (MemoryOwner<byte>, int)>(((MemoryOwner<byte>, int) input) => {
                MemoryOwner<byte> buffer;
                if (compress)
                {
                    buffer = compressionController.Compress(input.Item1);
                }
                else
                {
                    buffer = compressionController.Decompress(input.Item1);
                }

                input.Item1.Dispose();
                Console.WriteLine($"Transform {input.Item2} Length:{input.Item1.Length}");

                return (buffer, input.Item2);
            }, execOptions);

            var write = new ActionBlock<(MemoryOwner<byte>, int)>(((MemoryOwner<byte>, int) input) => {
                using Stream stream = File.Open(outputFile, FileMode.Append);
                {
                    stream.Write(input.Item1.Span);
                    input.Item1.Dispose();
                }
                Console.WriteLine($"Action {input.Item2} Length:{input.Item1.Length}");
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1 });

            DataflowLinkOptions linkOption = new DataflowLinkOptions() { PropagateCompletion = true };
            transform.LinkTo(write, linkOption);

            long fileLength = new FileInfo(inputFile).Length;
            MemoryOwner<byte> buffer;
            using (Stream stream = File.OpenRead(inputFile))
            {
                for (int i = 0; i < fileLength / chunkLength; i++)
                {
                    buffer = MemoryOwner<byte>.Allocate(chunkLength);
                    stream.Read(buffer.Span);
                    await transform.SendAsync((buffer, i));
                    Console.WriteLine($"Send {i}");
                }
                if (stream.Length - stream.Position > 0)
                {
                    int i = -1;
                    buffer = MemoryOwner<byte>.Allocate((int)(stream.Length - stream.Position));
                    stream.Read(buffer.Span);
                    await transform.SendAsync((buffer, i));
                    Console.WriteLine($"Send {i}");
                }
            }

            transform.Complete();

            await write.Completion;

            return true;
        }
    }
}
