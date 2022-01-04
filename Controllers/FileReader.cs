using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.IO;

namespace GZipTest.Controllers
{
    /// <summary>
    /// Perform reading operation from decompressed source file
    /// </summary>
    class FileReader
    {
        /// <summary>
        /// Default chunk length to pass as async enumerable
        /// </summary>
        protected static readonly int chunkLength = 1024*1024*100;

        /// <summary>
        /// Reading operation from source file. The file is read in chunks.
        /// </summary>
        /// <param name="inputFilename">Filename of original file to be processed</param>
        /// <returns>Chunks of data to be processed</returns>
        public async IAsyncEnumerable<DataChunk> Read(string inputFilename)
        {
            long fileLength = new FileInfo(inputFilename).Length;
            int overallChunksCount = (int)Math.Ceiling((double)fileLength / chunkLength);
            MemoryOwner<byte> buffer;
            using (Stream stream = File.OpenRead(inputFilename))
            {
                int chunksCnt = 0;
                for (; chunksCnt < fileLength / chunkLength; chunksCnt++)
                {
                    buffer = MemoryOwner<byte>.Allocate(chunkLength);
                    await stream.ReadAsync(buffer.Memory);
                    yield return new DataChunk() { uncompressedData = buffer, orderNum = chunksCnt, chunksCount = overallChunksCount, offset = chunksCnt*chunkLength, length = buffer.Length };
                }
                if (stream.Length - stream.Position > 0)
                {
                    buffer = MemoryOwner<byte>.Allocate((int)(stream.Length - stream.Position));
                    await stream.ReadAsync(buffer.Memory);
                    yield return new DataChunk() { uncompressedData = buffer, orderNum = chunksCnt, chunksCount = overallChunksCount, offset = chunksCnt * chunkLength, length = buffer.Length };
                }
            }
        }
    }
}
