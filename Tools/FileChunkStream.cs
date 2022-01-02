using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest.Tools
{
    public class FileChunkStream : FileStream
    {
        private long length;
        private long initialOffset;
        internal FileChunkStream(String path, FileMode mode, FileAccess access, FileShare share, long length, long offset) : base(path, mode, access, share)
        {
            this.Seek(offset, SeekOrigin.Begin);
            this.length = length;
            this.initialOffset = offset;
        }

        public override int Read([In, Out] byte[] array, int offset, int count)
        {
            long pos = this.Position;
            if (length + initialOffset < count + pos)
            {
                count = (int)(length + initialOffset - pos);
            }
            return base.Read(array, offset, count);
        }
    }
}
