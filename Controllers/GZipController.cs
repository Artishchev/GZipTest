using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest.Controllers
{
    class GZipController : ICompressionController
    {
        public MemoryOwner<byte> Compress(MemoryOwner<byte> uncompressedBytes)
        {
            return CompressDecompress(uncompressedBytes, true);
        }

        public MemoryOwner<byte> Decompress(MemoryOwner<byte> compressedBytes)
        {
            return CompressDecompress(compressedBytes, false);
        }

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
                    using (GZipStream decompressionStream = new GZipStream(inputBytes.AsStream(), workmode))
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
