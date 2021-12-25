using GZipTest.Models;
using System.Collections.Generic;

namespace GZipTest.Controllers
{
    /// <summary>
    /// Interface describes data reading operation from original file
    /// </summary>
    interface IDataReader
    {
        /// <summary>
        /// Reading from original file
        /// </summary>
        /// <param name="uncompressedFilename">Original filename</param>
        /// <returns>Async enumerable of data chunks</returns>
        IAsyncEnumerable<DataChunk> Read(string uncompressedFilename);
    }
}
