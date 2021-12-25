using GZipTest.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GZipTest
{
    /// <summary>
    /// Test application for compressing and decompressing files block-by-block using System.IO.Compression.GzipStream
    /// </summary>
    class Program
    {
        /// <summary>
        /// Instance of main pipeline controller
        /// </summary>
        static PiplineController piplineController = new PiplineController();

        static async Task<int> Main(string[] args)
        {
            int exitCode = 1;
            bool GracefulCancel = false;
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                if (!GracefulCancel)
                    Environment.Exit(1);
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
                GracefulCancel = false;
            };
            try
            {
                if(args.Length < 3 
                    || Array.Exists(new string[4] { "/?", "/h", "-help", "--help" }, input => args[0] == input) 
                    || args[0].ToLower() != "compress" 
                    && args[0].ToLower() != "decompress")
                {
                    Console.WriteLine(helpInfo);
                    return exitCode;
                }

                string inputFile = args[1];
                string outputFile = args[2];
                bool compress = args[0] == "compress";

                if (!File.Exists(inputFile))
                {
                    Console.WriteLine($"Input file not found '{inputFile}'. Operation aborted.");
                    return exitCode;
                }

                if (File.Exists(outputFile))
                {
                    Console.WriteLine($"The output file already exists. Do you want to overwrite it? (y/n)");
                    var i = Console.ReadKey();
                    Console.WriteLine(string.Empty);
                    if (i.Key == ConsoleKey.Y)
                    {
                        try
                        {
                            File.Delete(outputFile);
                        }
                        catch (IOException ex)
                        {
                            throw new DetailedMessageException("Error deleting output file. The output file is in use.", ex);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            throw new DetailedMessageException("Error deleting output file. The program does not have the required permission.", ex);
                        }
                        catch (Exception ex)
                        {
                            throw new DetailedMessageException("Error deleting output file.", ex);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Operation aborted.");
                        return exitCode;
                    }
                }

                GracefulCancel = true;
                await piplineController.PerformAction(inputFile, outputFile, compress, cts.Token);
                exitCode = 0;
            }
            catch (DetailedMessageException dme)
            {
                Console.WriteLine($"Error: {dme.Message}\r\nDetails:");
                Console.WriteLine(dme.InnerException);
            }
            catch (OperationCanceledException) // includes TaskCanceledException
            {
                Console.WriteLine("Operation cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception. Please send a content of this screen to the developer: artishev.ds@gmail.com");
                Console.WriteLine(ex);
            }
            return exitCode;
        }

        static string helpInfo =
@"
Test app for multithread file compression using GZip

Usage:
GZipTest.exe [Operation] [InputFilename] [OutputFilename]
    Operation - 'compress' to compress input file or 'decompress' to decompress input archive
    InputFilename - filename of original file to compress or arcive to decompress
    OutputFilename - filename of destination file

Vaersion: 0.1
Copyrights: Dmitriy Artishev (artishev.ds@gmail.com)
";
    }
}
