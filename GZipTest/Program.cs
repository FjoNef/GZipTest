using System;

namespace GZipTest
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Please, use the following parameters: GZipTest.exe compress/decompress input_file_name [output_file_name]");
                    Console.WriteLine("Press <Enter> to exit");
                    Console.ReadLine();
                    return 0;
                }

                string inputFileName = args[1];
                string outputFileName = null;
                if (args.Length > 2)
                    outputFileName = args[2];

                switch (args[0])
                {
                    case "compress":
                        Console.Write("Compressing...   0%");
                        GZipper.Compress(inputFileName, outputFileName);
                        Console.WriteLine("\nCompleted");
                        break;

                    case "decompress":
                        Console.Write("Decompressing...   0%");
                        GZipper.Decompress(inputFileName, outputFileName);
                        Console.WriteLine("\nCompleted");
                        break;

                    default:
                        Console.WriteLine("Please, use the following parameters: GZipTest.exe compress/decompress input_file_name [output_file_name]");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nError: {0}", ex.ToString());
                Console.WriteLine("Press <Enter> to exit");
                Console.ReadLine();
                return 1;
            }

            Console.WriteLine("Press <Enter> to exit");
            Console.ReadLine();
            return 0;
        }
    }
}