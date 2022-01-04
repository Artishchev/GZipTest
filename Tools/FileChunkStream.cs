using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GZipTest.Tools
{
    /// <summary>
    /// Custom file stream wrapper. limits the stream to a specific chunk of the file.
    /// </summary>
    public class FileChunkStream : FileStream
    {
        /// <summary>
        /// Chunk length
        /// </summary>
        private long length;

        /// <summary>
        /// Chunk starting point
        /// </summary>
        private long initialOffset;

        /// <summary>
        /// Creates stream of file chunk
        /// </summary>
        /// <param name="path">A relative or absolute path for the file that the current FileStream object will encapsulate.</param>
        /// <param name="mode">One of the enumeration values that determines how to open or create the file.</param>
        /// <param name="access">Flag that determines how the file can be accessed by the FileStreams</param>
        /// <param name="share">Flag that determines how the file will be shared by processes</param>
        /// <param name="length">Length of file chunk to be accessible</param>
        /// <param name="offset">Chunk starting point</param>
        internal FileChunkStream(String path, FileMode mode, FileAccess access, FileShare share, long length, long offset) : base(path, mode, access, share)
        {
            this.Seek(offset, SeekOrigin.Begin);
            this.length = length;
            this.initialOffset = offset;
        }

        /// <summary>
        /// Ovverrides FileStream.Read method to limit the file to specified chunk
        /// </summary>
        /// <param name="array">When this method returns, contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source</param>
        /// <param name="offset">The byte offset in array at which the read bytes will be placed.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
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
