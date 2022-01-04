using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

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

        public (long[],long) FindHeaders(DataChunk chunk)
        {
            List<long> chunkHeaderPositions = new List<long>();
            long possibleChunkheaderPositions = -1;
            IEnumerable<byte> byteFlags = gZipCurrentHeader.Distinct().ToArray();

            int currentPosition = 0;
            Span<byte> data = chunk.uncompressedData.Span;

            while (currentPosition < data.Length)
            {
                if (byteFlags.Contains(data[currentPosition]))
                {
                    // check for header
                    FindHeader(data, currentPosition, chunk.offset, chunkHeaderPositions);
                }
                currentPosition += gZipCurrentHeader.Length;
            }

            //checking end of chunk
            if (currentPosition >= data.Length)
                currentPosition = data.Length - 1;
            if (byteFlags.Contains(data[currentPosition]))
            {
                // check for header
                long possibleHeader = FindHeader(data, currentPosition, chunk.offset, chunkHeaderPositions);
                if (possibleHeader >= 0)
                    possibleChunkheaderPositions = possibleHeader + chunk.offset;
            }

            return (chunkHeaderPositions.ToArray(), possibleChunkheaderPositions);
        }

        public async Task<long[]> CheckPossibleHeaders(string compressedFilename, long[] possibleHeaders, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<long> chunkHeaderPositions = new List<long>();
            Array.Sort(possibleHeaders);
            using (Stream stream = File.OpenRead(compressedFilename))
            {
                foreach (long headerOffset in possibleHeaders)
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
