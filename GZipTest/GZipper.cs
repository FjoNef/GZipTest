using System;
using System.IO;

namespace GZipTest
{
    public static class GZipper
    {
        public static readonly Int32 BUFF_SIZE = 1024 * 1024;
        public static readonly Int32 PROC_COUNT = Environment.ProcessorCount;

        public static void Compress(String inputFileName, String outputFileName = null)
        {
            FileInfo inputFile = new FileInfo(inputFileName);
            FileInfo outputFile = new FileInfo(outputFileName ?? (inputFileName + ".gz2"));

            Int64 blocksCount = (Int64)Math.Ceiling((Double)inputFile.Length / BUFF_SIZE);

            Int32 threadsNumber = (Int32)Math.Min(PROC_COUNT, blocksCount);

            Compressor compressor = new Compressor(threadsNumber);

            compressor.Start(inputFile, outputFile);

            if (compressor.InnerException != null)
                throw compressor.InnerException;
        }

        public static void Decompress(String inputFileName, String outputFileName = null)
        {
            FileInfo inputFile = new FileInfo(inputFileName);
            FileInfo outputFile = new FileInfo(outputFileName ?? inputFile.FullName.Replace(".gz2", ""));

            if (inputFile.Extension != ".gz2")
                throw new FormatException("File is not a gzip archive");

            Int64 blocks = (Int64)Math.Ceiling((Double)inputFile.Length / BUFF_SIZE);

            Int32 threadsNumber = (Int32)Math.Min(PROC_COUNT, blocks);

            Decompressor decompressor = new Decompressor(threadsNumber);

            decompressor.Start(inputFile, outputFile);

            if (decompressor.InnerException != null)
                throw decompressor.InnerException;
        }
    }
}