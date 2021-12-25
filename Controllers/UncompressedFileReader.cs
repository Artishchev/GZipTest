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
    class UncompressedFileReader : IDataReader
    {
        /// <summary>
        /// Default chunk length to pass as async enumerable
        /// </summary>
        private static readonly int chunkLength = 1024*1024*10;

        /// <summary>
        /// Reading operation from decompressed source file
        /// </summary>
        /// <param name="uncompressedFilename">Filename of original file to be compressed</param>
        /// <returns>Chunks of data to be compressed</returns>
        public async IAsyncEnumerable<DataChunk> Read(string uncompressedFilename)
        {
            long fileLength = new FileInfo(uncompressedFilename).Length;
            int overallChunksCount = (int)Math.Ceiling((double)fileLength / chunkLength);
            MemoryOwner<byte> buffer;
            using (Stream stream = File.OpenRead(uncompressedFilename))
            {
                int chunksCnt = 0;
                for (; chunksCnt < fileLength / chunkLength; chunksCnt++)
                {
                    buffer = MemoryOwner<byte>.Allocate(chunkLength);
                    await stream.ReadAsync(buffer.Memory);
                    yield return new DataChunk() { uncompressedData = buffer, orderNum = chunksCnt, chunksCount = overallChunksCount };
                }
                if (stream.Length - stream.Position > 0)
                {
                    buffer = MemoryOwner<byte>.Allocate((int)(stream.Length - stream.Position));
                    await stream.ReadAsync(buffer.Memory);
                    yield return new DataChunk() { uncompressedData = buffer, orderNum = chunksCnt, chunksCount = overallChunksCount };
                }
            }
        }
    }
}
