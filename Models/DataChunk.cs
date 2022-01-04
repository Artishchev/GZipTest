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
        /// Input data to be processed
        /// </summary>
        public MemoryOwner<byte> inputData { get; set; }
        
        /// <summary>
        /// Output data to be written in to output file
        /// </summary>
        public MemoryOwner<byte> outputData { get; set; }

        /// <summary>
        /// Sequential chunk number
        /// </summary>
        public int orderNum { get; set; }

        /// <summary>
        /// Overall chunks count
        /// </summary>
        public int chunksCount { get; set; }

        /// <summary>
        /// Offset of the chunk in the file
        /// </summary>
        public long offset { get; set; }

        /// <summary>
        /// Length of the chunk
        /// </summary>
        public long length { get; set; }

        /// <summary>
        /// Temporary file name
        /// </summary>
        public string chunkFileName { get; set; }

        /// <summary>
        /// Disposes memory pool objects
        /// </summary>
        public void Dispose()
        {
            inputData?.Dispose();
            outputData?.Dispose();
        }
    }
}
