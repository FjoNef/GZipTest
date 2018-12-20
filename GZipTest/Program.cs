using System;

namespace GZipTest
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Please, use the following parameters: GZipTest.exe compress/decompress input_file_name [output_file_name]");
                    Console.WriteLine("Press <Enter> to exit");
                    Console.ReadLine();
                    return;
                }

                string inputFileName = args[1];
                string outputFileName = null;
                if (args.Length > 2)
                    outputFileName = args[2];

                switch (args[0])
                {
                    case "compress":
                        GZipper.Compress(inputFileName, outputFileName);
                        break;
                    case "decompress":
                        GZipper.Decompress(inputFileName, outputFileName);
                        break;
                    default:
                        Console.WriteLine("Please, use the following parameters: GZipTest.exe compress/decompress input_file_name [output_file_name]");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nError: {0}", ex.ToString());
            }

            Console.WriteLine("Press <Enter> to exit");
            Console.ReadLine();
        }
    }
}
