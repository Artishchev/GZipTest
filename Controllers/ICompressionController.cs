using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System.Threading.Tasks;

namespace GZipTest.Controllers
{
    /// <summary>
    /// Interface describes compression and decompression operations
    /// </summary>
    internal interface ICompressionController
    {
        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="uncompressedBytes">Input buffer to be compressed. Should be disposed after usage</param>
        /// <returns>Compressed data in buffer. Should be disposed after usage</returns>
        MemoryOwner<byte> Compress(MemoryOwner<byte> uncompressedBytes);

        /// <summary>
        /// Decompress data from source file chunk to the temporary file
        /// </summary>
        /// <param name="inputFile">File to be decompressed</param>
        /// <param name="outputFile">Destination file</param>
        /// <param name="dataChunk">Describes the chunk to be decompressed</param>
        /// <returns>Async task</returns>
        public Task DecompressToTempFilesAsync(string inputFile, string outputFile, DataChunk dataChunk);
    }
}
