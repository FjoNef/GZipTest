using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    public static class GZipper
    {
        private static readonly Int32 BUFF_SIZE = 1024 * 1024;
        private static readonly Int32 DEFLATE_BUFF_SIZE = 8 * 1024;
        private static readonly Int32 PROC_COUNT = Environment.ProcessorCount;

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

            using (GZipperStreamSyncronizer streamSyncronizer = new GZipperStreamSyncronizer(inputFile, outputFileName ?? (inputFileName + ".gz")))
            {
                streamSyncronizer.Blocks = (Int64)Math.Ceiling((Double)inputFile.Length / BUFF_SIZE);

                if (streamSyncronizer.Blocks > 0)
                {
                    Int32 threadsNumber = (Int32)Math.Min(PROC_COUNT, streamSyncronizer.Blocks);

                    Thread[] threads = new Thread[threadsNumber];

                    for (Int64 i = 0; i < threadsNumber; i++)
                    {
                        threads[i] = new Thread(CompressBlock);
                        threads[i].Start(streamSyncronizer);
                    }

                    foreach (Thread thread in threads)
                    {
                        thread.Join();
                    }
                }
                else
                {
                    CompressEmptyFile(streamSyncronizer);
                }
            }
        }

        private static void CompressEmptyFile(GZipperStreamSyncronizer streamSyncronizer)
        {
            streamSyncronizer.WriteToFile(_headerBytes, 0, _headerBytes.Length);
            streamSyncronizer.WriteToFile(new byte[8], 0, 8);
        }

        public static void Decompress(String inputFileName, String outputFileName = null)
        {
            FileInfo inputFile = new FileInfo(inputFileName);

            if (inputFile.Extension != ".gz")
                throw new FormatException("File is not a gzip archive");

            using (GZipperStreamSyncronizer streamSyncronizer = new GZipperStreamSyncronizer(inputFile, (outputFileName ?? inputFile.FullName.Replace(".gz", ""))))
            {
                streamSyncronizer.Blocks = (Int64)Math.Ceiling((Double)inputFile.Length / BUFF_SIZE);

                Int32 threadsNumber = (Int32)Math.Min(PROC_COUNT, streamSyncronizer.Blocks);

                Thread[] threads = new Thread[threadsNumber];

                for (Int64 i = 0; i < threadsNumber; i++)
                {
                    threads[i] = new Thread(DecompressBlock);
                    threads[i].Start(streamSyncronizer);
                }

                foreach (Thread thread in threads)
                {
                    thread.Join();
                }
            }
        }

        private static void CompressBlock(Object streamSyncronizer)
        {
            Int32 bytesReaded;
            Byte[] decompressedBuff = new Byte[BUFF_SIZE];

            GZipperStreamSyncronizer _streamSyncronizer = (GZipperStreamSyncronizer)streamSyncronizer;

            using (MemoryStream memStream = new MemoryStream())
            using (FileStream localInputStream = new FileStream(_streamSyncronizer.InputFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, DEFLATE_BUFF_SIZE))
            {
                Int64 currentBlock = _streamSyncronizer.CurrentBlockReaded;

                while (currentBlock < _streamSyncronizer.Blocks)
                {
                    Int64 fragmentStart = BUFF_SIZE * currentBlock;

                    localInputStream.Position = fragmentStart;

                    bytesReaded = localInputStream.Read(decompressedBuff, 0, decompressedBuff.Length);

                    using (GZipStream gzStream = new GZipStream(memStream, CompressionMode.Compress, true))
                    {
                        gzStream.Write(decompressedBuff, 0, bytesReaded);
                    }

                    _streamSyncronizer.WriteBlockToFile(memStream, currentBlock, true);

                    currentBlock = _streamSyncronizer.CurrentBlockReaded;
                }
            }
        }

        private static void DecompressBlock(Object streamSyncronizer)
        {
            GZipperStreamSyncronizer _streamSyncronizer = (GZipperStreamSyncronizer)streamSyncronizer;
            Byte[] compressedBuffer = new Byte[BUFF_SIZE + 2];

            using (MemoryStream memStream = new MemoryStream())
            using (FileStream localInputStream = new FileStream(_streamSyncronizer.InputFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, DEFLATE_BUFF_SIZE))
            {
                Int64 currentBlock = _streamSyncronizer.CurrentBlockReaded;

                while (currentBlock < _streamSyncronizer.Blocks)
                {
                    Int64 fragmentStart = BUFF_SIZE * currentBlock;

                    localInputStream.Position = fragmentStart;

                    //2 additional bytes in case of "magic numbers" separation
                    Int32 bytesReaded = localInputStream.Read(compressedBuffer, 0, BUFF_SIZE + 2);
                    localInputStream.Position -= bytesReaded;

                    for (Int64 i = 0; i < bytesReaded - 2;)
                    {
                        if (compressedBuffer[i] == 0x1F && compressedBuffer[i + 1] == 0x8B && compressedBuffer[i + 2] == 0x8)
                        {
                            localInputStream.Position = fragmentStart + i;
                            try
                            {

                                using (GZipStream gzStream = new GZipStream(localInputStream,
                                            CompressionMode.Decompress, true))
                                {
                                    Byte[] buffer = new Byte[DEFLATE_BUFF_SIZE * 10];
                                    Int32 read;
                                    while ((read = gzStream.Read(buffer, 0, buffer.Length)) != 0)
                                    {
                                        memStream.Write(buffer, 0, read);
                                        if (memStream.Length > BUFF_SIZE * 10)
                                            _streamSyncronizer.WriteBlockToFile(memStream, currentBlock, false);
                                    }
                                }

                                //The header of the next member may be in a previously inflated block (8Kb)
                                if ((localInputStream.Position - fragmentStart) >= DEFLATE_BUFF_SIZE)
                                    i = (localInputStream.Position - fragmentStart) - (DEFLATE_BUFF_SIZE - 1);
                                else
                                    i = (localInputStream.Position - fragmentStart);
                            }
                            catch (InvalidDataException)
                            {
                                i += 3;
                            }
                        }
                        else
                        {
                            i++;
                        }
                    }

                    _streamSyncronizer.WriteBlockToFile(memStream, currentBlock, true);
                    currentBlock = _streamSyncronizer.CurrentBlockReaded;
                }
            }
        }
    }
}