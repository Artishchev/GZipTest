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
            //string outputFile = "test.gz";
            //string inputFile = "input.pdf";
            //bool compress = true;

            string outputFile = "output.pdf";
            string inputFile = "test.gz";
            bool compress = false;


            Console.WriteLine($"Hello World! Processors: {Environment.ProcessorCount}");
            if (File.Exists(inputFile))
            {
                Console.WriteLine($"Input file not found {inputFile}");
            }

            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            await gZipTest.PerformAction(inputFile, outputFile, compress);
        }
    }
}
