using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest.Controllers
{
    interface IDataReader
    {
        IAsyncEnumerable<DataChunk> Read(string uncompressedFilename);
    }
}
