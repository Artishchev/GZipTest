using System;
using System.IO;
using System.Threading.Tasks;

namespace GZipTest
{
    class Program
    {
        static GZipTest gZipTest = new GZipTest();

        static async Task Main(string[] args)
        {
            if(args.Length < 3 
                || Array.Exists(new string[4] { "/?", "/h", "-help", "--help" }, input => args[0] == input) 
                || args[0].ToLower() != "compress" 
                && args[0].ToLower() != "decompress")
            {
                Console.WriteLine(helpInfo);
                return;
            }

            string inputFile = args[1];
            string outputFile = args[2];
            bool compress = args[0] == "compress";


            Console.WriteLine($"Hello World! Processors: {Environment.ProcessorCount}");
            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Input file not found '{inputFile}'. Operation aborted.");
                return;
            }

            if (File.Exists(outputFile))
            {
                Console.WriteLine($"The output file already exists. Do you want to overwrite it? (y/n)");
                var i = Console.ReadKey();
                Console.WriteLine(string.Empty);
                if (i.KeyChar.ToString().ToLower() == "y")
                    File.Delete(outputFile);
                else
                {
                    Console.WriteLine($"Operation aborted.");
                    return;
                }
            }

            await gZipTest.PerformAction(inputFile, outputFile, compress);
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
