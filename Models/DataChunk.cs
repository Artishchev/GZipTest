using Microsoft.Toolkit.HighPerformance.Buffers;
using System;

namespace GZipTest.Models
{
    /// <summary>
    /// DTO describes chunk of data to be processed
    /// </summary>
    public class DataChunk : IDisposable
    {
        /// <summary>
        /// Uncompressed data to be compressed or to be written to output file after decompression
        /// </summary>
        public MemoryOwner<byte> uncompressedData { get; set; }
        
        /// <summary>
        /// Compressed data to be uncompressed or to be written to output file after compression
        /// </summary>
        public MemoryOwner<byte> compressedData { get; set; }

        /// <summary>
        /// Sequential chunk number
        /// </summary>
        public int orderNum { get; set; }

        /// <summary>
        /// Disposes memory pool objects
        /// </summary>
        public void Dispose()
        {
            uncompressedData?.Dispose();
            compressedData?.Dispose();
        }
    }
}
