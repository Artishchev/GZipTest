using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace GZipTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //string outputFile = "test.gz";
            //string inputFile = "input.pdf";
            //CompressionMode workmode = CompressionMode.Compress;

            string outputFile = "output.pdf";
            string inputFile = "test.gz";
            CompressionMode workmode = CompressionMode.Decompress;

            int chunkLength = 100000000;
            Console.WriteLine($"Hello World! Processors: {Environment.ProcessorCount}");
            if (File.Exists(inputFile))
            {
                Console.WriteLine($"Input file not found {inputFile}");
            }

            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            ExecutionDataflowBlockOptions execOptions = new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1 /*Environment.ProcessorCount*/, BoundedCapacity = Environment.ProcessorCount };

            var transform = new TransformBlock<(MemoryOwner<byte>, int), (MemoryOwner<byte>, int)>(async ((MemoryOwner<byte>, int) input) => {
                MemoryOwner<byte> buffer;
                using (MemoryStream memStr = new MemoryStream())
                {
                    if (workmode == CompressionMode.Compress)
                    {
                        using (GZipStream compressionStream = new GZipStream(memStr, workmode, true))
                        {
                            input.Item1.AsStream().CopyTo(compressionStream);
                        }
                    }
                    else
                    {
                        using (GZipStream decompressionStream = new GZipStream(input.Item1.AsStream(), workmode))
                        {
                            decompressionStream.CopyTo(memStr);
                        }
                    }
                    buffer = MemoryOwner<byte>.Allocate((int)memStr.Length);
                    memStr.Seek(0, SeekOrigin.Begin);
                    memStr.Read(buffer.Span);
                    input.Item1.Dispose();
                }
                Console.WriteLine($"Transform {input.Item2} Length:{input.Item1.Length}");

                return (buffer, input.Item2);
            }, execOptions);

            var write = new ActionBlock<(MemoryOwner<byte>, int)>(async ((MemoryOwner<byte>, int) input) => {
                using Stream stream = File.Open(outputFile, FileMode.Append);
                {
                    stream.Write(input.Item1.Span);
                    input.Item1.Dispose();
                }
                Console.WriteLine($"Action {input.Item2} Length:{input.Item1.Length}");
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1 });

            DataflowLinkOptions linkOption = new DataflowLinkOptions() { PropagateCompletion = true };
            transform.LinkTo(write, linkOption);


            long fileLength = new System.IO.FileInfo(inputFile).Length;
            
            MemoryOwner<byte> buffer;
            using (Stream stream = File.OpenRead(inputFile))
            {
                for (int i = 0; i < fileLength / chunkLength; i++)
                {
                    //stream.Seek(chunkLength * i, SeekOrigin.Begin);
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

            await transform.Completion;
        }
    }
}
