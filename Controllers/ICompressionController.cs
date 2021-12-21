using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest.Controllers
{
    internal interface ICompressionController
    {
        MemoryOwner<byte> Compress(MemoryOwner<byte> uncompressedBytes);
        MemoryOwner<byte> Decompress(MemoryOwner<byte> compressedBytes);
    }
}
