using Microsoft.Toolkit.HighPerformance.Buffers;
using System;

namespace GZipTest.Models
{
    public class DataChunk : IDisposable
    {
        public MemoryOwner<byte> uncompressedData { get; set; }
        public MemoryOwner<byte> compressedData { get; set; }
        public int orderNum { get; set; }

        public void Dispose()
        {
            uncompressedData?.Dispose();
            compressedData?.Dispose();
        }
    }
}
