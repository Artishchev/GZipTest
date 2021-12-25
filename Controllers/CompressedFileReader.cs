using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.IO;

namespace GZipTest.Controllers
{
    /// <summary>
    /// Perform reading operation from compressed source file
    /// </summary>
    class CompressedFileReader : IDataReader
    {
        /// <summary>
        /// Magic numbers from a beginning of a GZip header
        /// </summary>
        private static readonly byte[] gZipHeaderMagicNumbers = new byte[2] { 0x1f, 0x8b };

        /// <summary>
        /// Default reading buffer size
        /// </summary>
        private static int readBufferSize = 1024 * 1024 * 1;

        /// <summary>
        /// GZip header specific for current file
        /// </summary>
        private static byte[] gZipCurrentHeader;

        /// <summary>
        /// Perform reading operation from compressed file. Reading data with help of memory buffer and automatically determinants a GZip chunks
        /// </summary>
        /// <param name="compressedFilename">Filename of original compressed file</param>
        /// <returns>GZip formatted data chunk</returns>
        public async IAsyncEnumerable<DataChunk> Read(string compressedFilename)
        {
            long bytesEqualToHeader = 0;
            int chunksCount = 0;
            bool skipFirstHeader = false;
            List<MemoryOwner<byte>> buffersList = new List<MemoryOwner<byte>>();
            using (FileStream stream = File.OpenRead(compressedFilename))
            {
                MemoryOwner<byte> readBuffer = MemoryOwner<byte>.Allocate(readBufferSize);
                while (await stream.ReadAsync(readBuffer.Memory) != 0)
                {
                    // Remember the header specific to this file
                    if (gZipCurrentHeader == null)
                    {
                        if (readBuffer.Span[0] == gZipHeaderMagicNumbers[0] && readBuffer.Span[1] == gZipHeaderMagicNumbers[1])
                        {
                            gZipCurrentHeader = readBuffer.Span.Slice(0, 10).ToArray();
                            skipFirstHeader = true;
                        }
                        else
                        {
                            throw new DetailedMessageException("Input file is not a GZip formatted archive.", new FormatException("Unsupported file format"));
                        }
                    }

                    // Reading buffer byte by byte
                    for (int i = 0; i < readBuffer.Length; i++)
                    {
                        // Skipping first header
                        if (skipFirstHeader)
                        {
                            skipFirstHeader = false;
                            i = gZipCurrentHeader.Length - 1;
                        }

                        if (readBuffer.Span[i] == gZipCurrentHeader[bytesEqualToHeader])
                        {
                            bytesEqualToHeader++;
                            if (bytesEqualToHeader >= gZipCurrentHeader.Length)
                            {
                                // We found next header
                                bytesEqualToHeader = 0;
                                buffersList.Add(readBuffer);
                                var concatResult = ConcatBuffers(buffersList, i + 1);
                                yield return new DataChunk() { compressedData = concatResult.Item1, orderNum = chunksCount++ };
                                readBuffer = concatResult.Item2;
                                i = gZipCurrentHeader.Length;

                                // If resulting chunk size was greater than a read buffer 
                                // expanding size of read buffer with additional 5%
                                if (readBufferSize < concatResult.Item1.Length)
                                {
                                    readBufferSize = (int)(concatResult.Item1.Length * 1.05);
                                }
                            }
                        }
                        else if (bytesEqualToHeader > 0)
                        {
                            bytesEqualToHeader = 0;
                        }
                    }

                    // Adding current buffer to buffer list to concat it later with next buffer
                    buffersList.Add(readBuffer);
                    readBuffer = MemoryOwner<byte>.Allocate(readBufferSize);
                }

                readBuffer.Dispose();

                // Adding last buffer to the results
                if (buffersList.Count > 0)
                {
                    var concatResult = ConcatBuffers(buffersList, buffersList[buffersList.Count - 1].Length - 1);
                    yield return new DataChunk() { compressedData = concatResult.Item1, orderNum = chunksCount++ };
                }
            }
        }

        /// <summary>
        /// Concatenate buffers to create complete GZip formatted data chunk
        /// </summary>
        /// <param name="buffersList">Intermediate read buffers</param>
        /// <param name="positionInLastBuffer">Pointer to the position in the last buffer where the next header was found</param>
        /// <returns>First item - resulting GZip data chunk. Second item - last buffer to be processed further</returns>
        private (MemoryOwner<byte>, MemoryOwner<byte>) ConcatBuffers(List<MemoryOwner<byte>> buffersList, int positionInLastBuffer)
        {
            // Calculating summary buffer length
            int chunkBufferLength = 0;
            for (int i = 0; i < buffersList.Count - 1; i++)
            {
                chunkBufferLength += buffersList[i].Length;
            }
            chunkBufferLength += positionInLastBuffer - gZipCurrentHeader.Length;
            MemoryOwner<byte> chunkBuffer = MemoryOwner<byte>.Allocate(chunkBufferLength);
            MemoryOwner<byte> currentChunkBuffer;

            // Concating all buffers from list except two last
            int bufferIndex = 0;
            for (int i = 0; i < buffersList.Count - 2; i++)
            {
                buffersList[i].Span.CopyTo(chunkBuffer.Span.Slice(bufferIndex, buffersList[i].Length));
                bufferIndex += buffersList[i].Length;
            }

            if (positionInLastBuffer <= gZipCurrentHeader.Length)
            {
                // Header was between buffers
                int lastChunkLength = buffersList[buffersList.Count - 2].Length - gZipCurrentHeader.Length + positionInLastBuffer;
                buffersList[buffersList.Count - 2].Span.CopyTo(chunkBuffer.Span.Slice(bufferIndex, lastChunkLength));
                currentChunkBuffer = MemoryOwner<byte>.Allocate(buffersList[buffersList.Count - 1].Length - positionInLastBuffer + gZipCurrentHeader.Length);
                gZipCurrentHeader.CopyTo(currentChunkBuffer.Memory);
                buffersList[buffersList.Count - 1].Span.Slice(positionInLastBuffer).CopyTo(currentChunkBuffer.Span.Slice(gZipCurrentHeader.Length));
            }
            else
            {
                // Header was in last buffer
                if (buffersList.Count > 1)
                {
                    buffersList[buffersList.Count - 2].Span.CopyTo(chunkBuffer.Span.Slice(bufferIndex, buffersList[buffersList.Count - 2].Length));
                    bufferIndex += buffersList[buffersList.Count - 2].Length;
                }
                buffersList[buffersList.Count - 1].Span.Slice(0, positionInLastBuffer - gZipCurrentHeader.Length).CopyTo(chunkBuffer.Span.Slice(bufferIndex, positionInLastBuffer - gZipCurrentHeader.Length));
                currentChunkBuffer = MemoryOwner<byte>.Allocate(buffersList[buffersList.Count - 1].Length - positionInLastBuffer + gZipCurrentHeader.Length);
                buffersList[buffersList.Count - 1].Span.Slice(positionInLastBuffer - gZipCurrentHeader.Length).CopyTo(currentChunkBuffer.Span);
            }

            // Disposing old buffers
            foreach (var buffer in buffersList)
            {
                buffer.Dispose();
            }
            buffersList.RemoveRange(0, buffersList.Count);

            return (chunkBuffer, currentChunkBuffer);
        }
    }
}
