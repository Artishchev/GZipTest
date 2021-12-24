using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System.Collections.Generic;
using System.IO;

namespace GZipTest.Controllers
{
    class CompressedFileReader : IDataReader
    {
        private static readonly int chunkLength = 1000000;
        public async IAsyncEnumerable<DataChunk> Read(string uncompressedFilename)
        {
            long fileLength = new FileInfo(uncompressedFilename).Length;
            MemoryOwner<byte> buffer;
            using (Stream stream = File.OpenRead(uncompressedFilename))
            {
                int chunksCount = 0;
                for (; chunksCount < fileLength / chunkLength; chunksCount++)
                {
                    buffer = MemoryOwner<byte>.Allocate(chunkLength);
                    await stream.ReadAsync(buffer.Memory);
                    yield return new DataChunk() { uncompressedData = buffer, orderNum = chunksCount };
                }
                if (stream.Length - stream.Position > 0)
                {
                    buffer = MemoryOwner<byte>.Allocate((int)(stream.Length - stream.Position));
                    await stream.ReadAsync(buffer.Memory);
                    yield return new DataChunk() { uncompressedData = buffer, orderNum = chunksCount };
                }
            }
        }
    }
}
