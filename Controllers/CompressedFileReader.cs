using GZipTest.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GZipTest.Controllers
{
    /// <summary>
    /// Perform reading operation from compressed source file
    /// </summary>
    class CompressedFileReader : FileReader
    {
        /// <summary>
        /// Magic numbers from a beginning of a GZip header
        /// </summary>
        private static readonly byte[] gZipHeaderMagicNumbers = new byte[2] { 0x1f, 0x8b };

        /// <summary>
        /// GZip header specific for current file
        /// </summary>
        private byte[] gZipCurrentHeader;

        /// <summary>
        /// Check the headers in the beginning of the compressed file
        /// </summary>
        /// <param name="compressedFilename">File to be checked</param>
        /// <param name="cancellationToken">In case of errors or termination by user</param>
        /// <returns>Async task</returns>
        /// <exception cref="DetailedMessageException">Throws with internal FormatException when header not found</exception>
        public async Task CheckCompressedFileFormatAsync(string compressedFilename, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte[] buffer = new byte[10];
            bool fileFormatMatch = true;
            using (Stream stream = File.OpenRead(compressedFilename))
            {
                int readBytes = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (readBytes == buffer.Length && buffer[0] == gZipHeaderMagicNumbers[0] && buffer[1] == gZipHeaderMagicNumbers[1])
                {
                    fileFormatMatch = true;
                    gZipCurrentHeader = buffer;
                }
            }
            if (!fileFormatMatch)
                throw new DetailedMessageException("Input file is not a GZip formatted archive.", new FormatException("Unsupported file format"));
        }

        /// <summary>
        /// Search for gzip headers in the file
        /// </summary>
        /// <param name="chunk">File chunk to be checked</param>
        /// <param name="resultHeaders">Found gzip headers</param>
        /// <returns>possible header at the end of chunk</returns>
        public long FindHeaders(DataChunk chunk, List<long> resultHeaders)
        {
            long possibleChunkheaderPositions = -1;
            IEnumerable<byte> byteFlags = gZipCurrentHeader.Distinct().ToArray();

            int currentPosition = 0;
            Span<byte> data = chunk.inputData.Span;

            while (currentPosition < data.Length)
            {
                if (byteFlags.Contains(data[currentPosition]))
                {
                    // check for header
                    FindHeader(data, currentPosition, chunk.offset, resultHeaders);
                }
                currentPosition += gZipCurrentHeader.Length;
            }

            //checking end of chunk
            if (currentPosition >= data.Length)
                currentPosition = data.Length - 1;
            if (byteFlags.Contains(data[currentPosition]))
            {
                // check for header
                long possibleHeader = FindHeader(data, currentPosition, chunk.offset, resultHeaders);
                if (possibleHeader >= 0)
                    possibleChunkheaderPositions = possibleHeader + chunk.offset;
            }

            chunk.inputData.Dispose();

            return possibleChunkheaderPositions;
        }

        /// <summary>
        /// Checks a bytes at the border of the read chunks where could be a real headers
        /// </summary>
        /// <param name="compressedFilename">File to be checked</param>
        /// <param name="possibleHeaders">Array with possible headers starting positions</param>
        /// <param name="cancellationToken">In case of errors or termination by user</param>
        /// <returns>Confirmed headers</returns>
        public async Task<long[]> CheckPossibleHeaders(string compressedFilename, long[] possibleHeaders, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<long> chunkHeaderPositions = new List<long>();
            Array.Sort(possibleHeaders);
            using (Stream stream = File.OpenRead(compressedFilename))
            {
                foreach (long headerOffset in possibleHeaders.Distinct())
                {
                    if (headerOffset < stream.Length - gZipCurrentHeader.Length)
                    {
                        stream.Seek(headerOffset, SeekOrigin.Begin);
                        byte[] buffer = new byte[gZipCurrentHeader.Length + 1];
                        int readBytes = await stream.ReadAsync(buffer, cancellationToken);
                        if (readBytes > gZipCurrentHeader.Length)
                        {
                            FindHeader(buffer, 0, headerOffset, chunkHeaderPositions);
                        }
                    }
                }
            }
            return chunkHeaderPositions.ToArray();
        }

        /// <summary>
        /// Searches for headers in data chunk at the current position
        /// </summary>
        /// <param name="data">Compressed data chunk</param>
        /// <param name="currentPosition">A place where there might be headlines nearby</param>
        /// <param name="chunkOffset">Offset of the compressed data chunk. Used to address headers from the beginning of the file</param>
        /// <param name="chunkHeaderPositions">Found headers</param>
        /// <returns>Possible header position at the end of chunk</returns>
        private long FindHeader(Span<byte> data, int currentPosition, long chunkOffset, List<long> chunkHeaderPositions)
        {
            long possibleHeader = -1;
            int chkStart = currentPosition - gZipCurrentHeader.Length;
            if (chkStart < 0)
                chkStart = 0;
            int chkEnd = chkStart + gZipCurrentHeader.Length;
            if (chkEnd > data.Length - 1)
                chkEnd = data.Length - 1;
            int bytesEqualToHeader = 0;
            for (int chkCurrent = chkStart; (chkCurrent <= chkEnd || bytesEqualToHeader > 0) && chkCurrent < data.Length; chkCurrent++)
            {
                if (data[chkCurrent] == gZipCurrentHeader[bytesEqualToHeader])
                    bytesEqualToHeader++;
                else
                    bytesEqualToHeader = 0;
                if (bytesEqualToHeader >= gZipCurrentHeader.Length)
                {
                    chunkHeaderPositions.Add(chunkOffset + chkCurrent - gZipCurrentHeader.Length + 1);
                    bytesEqualToHeader = 0;
                }
                if (chkCurrent == data.Length - 1 && bytesEqualToHeader > 0)
                    possibleHeader = chkCurrent - bytesEqualToHeader + 1;
            }

            return possibleHeader;
        }
    }
}
