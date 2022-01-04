using GZipTest.Models;
using GZipTest.Tools;
using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

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
            MemoryOwner<byte> buffer;
            using (MemoryStream memStr = new MemoryStream())
            {
                using (GZipStream compressionStream = new GZipStream(memStr, CompressionMode.Compress, true))
                {
                    uncompressedBytes.AsStream().CopyTo(compressionStream);
                }
                buffer = MemoryOwner<byte>.Allocate((int)memStr.Length);
                memStr.Seek(0, SeekOrigin.Begin);
                memStr.Read(buffer.Span);
            }
            return buffer;
        }

        /// <summary>
        /// Decompress data from source file chunk to the temporary file
        /// </summary>
        /// <param name="inputFile">File to be decompressed</param>
        /// <param name="outputFile">Destination file</param>
        /// <param name="dataChunk">Describes the chunk to be decompressed</param>
        /// <returns>Async task</returns>
        public async Task DecompressToTempFilesAsync(string inputFile, string outputFile, DataChunk dataChunk)
        {
            string filename = Path.Combine(Path.GetTempPath(), outputFile + dataChunk.orderNum.ToString());
            using (FileChunkStream fileChunk = new FileChunkStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, dataChunk.length, dataChunk.offset))
            using (FileStream writeStream = File.OpenWrite(filename))
            using (GZipStream decompressionStream = new GZipStream(fileChunk, CompressionMode.Decompress, true))
            {
                await decompressionStream.CopyToAsync(writeStream);
            }
            dataChunk.chunkFileName = filename;
        }
    }
}
