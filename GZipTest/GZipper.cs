using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    public static class GZipper
    {
        public static readonly Int32 BUFF_SIZE = 1024 * 1024;
        public static readonly Int32 PROC_COUNT = Environment.ProcessorCount;

        private static readonly byte[] _headerBytes = new byte[]
        {
            0x1F, // ID1
            0x8B, // ID2
            0x8,  // CM = deflate
            0,    // FLG, no text, no crc, no extra, no name, no comment
 
            // MTIME (Modification Time) - no time available
            0,
            0,
            0,
            0, 
 
            // XFL
            // 2 = compressor used max compression, slowest algorithm
            // 4 = compressor used fastest algorithm
            4,
 
            // OS: 0 = FAT filesystem (MS-DOS, OS/2, NT/Win32)
            0
        };

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

        private static void CompressEmptyFile()
        {
            //streamSyncronizer.WriteToFile(_headerBytes, 0, _headerBytes.Length);
            //streamSyncronizer.WriteToFile(new byte[8], 0, 8);
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