using GZipTest.Controllers;
using GZipTest.Models;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Linq;

namespace GZipTest.Controllers
{
    /// <summary>
    /// Controller for DataFlow Pipelines
    /// Initializes the processing pipeline
    /// </summary>
    internal abstract class PiplineController
    {
        /// <summary>
        /// Perform compression and decompression operations in GZip format
        /// </summary>
        internal static ICompressionController compressionController = new GZipController();

        /// <summary>
        /// Perform compression and decompression pipelines
        /// </summary>
        /// <param name="inputFile">Original filename. File to be compressed or decompressed depending on <paramref name="compress"/> flag</param>
        /// <param name="outputFile">Destination filename</param>
        /// <param name="cancellationToken">In case of errors or termination by user</param>
        /// <returns>True on sucess. False on errors</returns>
        public abstract Task<bool> PerformAction(string inputFile, string outputFile, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Displays current progress to the console
        /// </summary>
        /// <param name="progress">Current processed item</param>
        /// <param name="tot">Total items</param>
        protected static void ProgressBar(int progress, int tot)
        {
            if (tot < progress)
            {
                progress = tot;
            }
            //draw empty progress bar
            Console.CursorVisible = false;
            Console.CursorLeft = 0;
            Console.Write("["); //start
            Console.CursorLeft = 32;
            Console.Write("]"); //end
            Console.CursorLeft = 1;
            float onechunk = 30.0f / tot;

            //draw filled part
            int position = 1;
            for (int i = 0; i <= onechunk * progress; i++)
            {
                Console.CursorLeft = position++;
                Console.Write("+");
            }

            //draw unfilled part
            for (int i = position; i <= 31; i++)
            {
                Console.CursorLeft = position++;
                Console.Write("-");
            }

            //draw totals
            Console.CursorLeft = 35;
            Console.Write(progress.ToString() + " of " + tot.ToString() + "    "); //blanks at the end remove any excess
        }
    }
}
