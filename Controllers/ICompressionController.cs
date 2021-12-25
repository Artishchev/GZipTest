using Microsoft.Toolkit.HighPerformance.Buffers;

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
        /// Decompress data
        /// </summary>
        /// <param name="compressedBytes">Input buffer to be decompressed. Should be disposed after usage</param>
        /// <returns>Decompressed data in buffer. Should be disposed after usage</returns>
        MemoryOwner<byte> Decompress(MemoryOwner<byte> compressedBytes);
    }
}
