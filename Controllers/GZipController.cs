using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System.IO;
using System.IO.Compression;

namespace GZipTest.Controllers
{
    /// <summary>
    /// Compression and decompression operations in GZip format
    /// </summary>
    class GZipController : ICompressionController
    {
        /// <summary>
        /// Compress data to GZip format
        /// </summary>
        /// <param name="uncompressedBytes">Input buffer to be compressed. Should be disposed after usage</param>
        /// <returns>Compressed data in buffer. Should be disposed after usage</returns>
        public MemoryOwner<byte> Compress(MemoryOwner<byte> uncompressedBytes)
        {
            return CompressDecompress(uncompressedBytes, true);
        }

        /// <summary>
        /// Decompress data from GZip format
        /// </summary>
        /// <param name="compressedBytes">Input buffer to be decompressed. Should be disposed after usage</param>
        /// <returns>Decompressed data in buffer. Should be disposed after usage</returns>
        public MemoryOwner<byte> Decompress(MemoryOwner<byte> compressedBytes)
        {
            return CompressDecompress(compressedBytes, false);
        }

        /// <summary>
        /// Perform compression and decompression using GZip format
        /// </summary>
        /// <param name="inputBytes">Input buffer to be processed</param>
        /// <param name="compress">Compression if true and decompression if false</param>
        /// <returns>Input buffer after processing</returns>
        private MemoryOwner<byte> CompressDecompress(MemoryOwner<byte> inputBytes, bool compress)
        {
            MemoryOwner<byte> buffer;
            CompressionMode workmode = compress ? CompressionMode.Compress : CompressionMode.Decompress; 
            using (MemoryStream memStr = new MemoryStream())
            {
                if (compress)
                {
                    using (GZipStream compressionStream = new GZipStream(memStr, workmode, true))
                    {
                        inputBytes.AsStream().CopyTo(compressionStream);
                    }
                }
                else
                {
                    using (GZipStream decompressionStream = new GZipStream(inputBytes.AsStream(), workmode, true))
                    {
                        decompressionStream.CopyTo(memStr);
                    }
                }
                buffer = MemoryOwner<byte>.Allocate((int)memStr.Length);
                memStr.Seek(0, SeekOrigin.Begin);
                memStr.Read(buffer.Span);
            }
            return buffer;
        }
    }
}
