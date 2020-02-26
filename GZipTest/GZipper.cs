using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    internal static class GZipper
    {
        private static readonly Int32 BUFF_SIZE = 1024 * 1024;
        private static readonly Int32 PROC_COUNT = Environment.ProcessorCount;
        private static readonly Object StreamLock = new Object();
        private static readonly Object QueueLock = new Object();

        private static Int64 _currentBlockReaded;
        private static Int64 _currentBlockWrited;
        private static Int64 _blocks;
        private static FileStream _outputStream;

        public static void Compress(String inputFileName, String outputFileName = null)
        {
            try
            {
                Console.Write("Compressing");

                FileInfo inputFile = new FileInfo(inputFileName);

                _currentBlockWrited = 0;
                _currentBlockReaded = 0;

                using (_outputStream = new FileStream((outputFileName ?? inputFile.FullName) + ".gz", FileMode.CreateNew, FileAccess.Write))
                {
                    _blocks = (Int64)Math.Ceiling((Double)inputFile.Length / BUFF_SIZE);

                    Int32 threadsNumber = (Int32)Math.Min(PROC_COUNT, _blocks);

                    Thread[] threads = new Thread[threadsNumber];

                    for (Int64 i = 0; i < threadsNumber; i++)
                    {
                        threads[i] = new Thread(CompressBlock);
                        threads[i].Start(inputFile);
                    }

                    foreach (Thread thread in threads)
                    {
                        thread.Join();
                    }
                }
                Console.WriteLine("Completed");
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("\nNo such file or directory");
            }
        }

        public static void Decompress(String inputFileName, String outputFileName = null)
        {
            try
            {
                FileInfo inputFile = new FileInfo(inputFileName);

                if (inputFile.Extension == ".gz")
                {
                    Console.Write("Decompressing");

                    _currentBlockWrited = 0;
                    _currentBlockReaded = 0;

                    using (_outputStream = File.Create((outputFileName ?? inputFile.FullName.Replace(".gz", ""))))
                    {
                        _blocks = (Int64)Math.Ceiling((Double)inputFile.Length / BUFF_SIZE);

                        Int32 threadsNumber = (Int32)Math.Min(PROC_COUNT, _blocks);

                        Thread[] threads = new Thread[threadsNumber];

                        for (Int64 i = 0; i < threadsNumber; i++)
                        {
                            threads[i] = new Thread(DecompressBlock);
                            threads[i].Start(inputFile);
                        }

                        foreach (Thread thread in threads)
                        {
                            thread.Join();
                        }
                    }
                    Console.WriteLine("Completed");
                }
                else
                {
                    Console.Write("The file is not a gzip archive");
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("\nNo such file or directory");
            }
        }

        private static void CompressBlock(Object inputFileInfo)
        {
            Int32 bytesReaded;
            Byte[] compressedBuff;
            Byte[] buff = new Byte[BUFF_SIZE];

            FileInfo inputFile = (FileInfo)inputFileInfo;

            using (FileStream localInputStream = inputFile.OpenRead())
            {
                Int64 currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;

                while (currentBlock <= _blocks)
                {
                    Int64 fragmentStart = BUFF_SIZE * currentBlock;

                    localInputStream.Position = fragmentStart;

                    bytesReaded = localInputStream.Read(buff, 0, buff.Length);

                    using (MemoryStream memStream = new MemoryStream())
                    {
                        using (GZipStream gzStream = new GZipStream(memStream, CompressionMode.Compress, true))
                        {
                            gzStream.Write(buff, 0, bytesReaded);
                        }
                        compressedBuff = memStream.ToArray();
                    }

                    Monitor.Enter(StreamLock);

                    while (currentBlock != _currentBlockWrited)
                    {
                        Monitor.Wait(StreamLock);
                    }

                    _outputStream.Write(compressedBuff, 0, compressedBuff.Length);
                    _currentBlockWrited++;

                    Monitor.PulseAll(StreamLock);

                    Monitor.Exit(StreamLock);

                    Console.Write(".");

                    currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;
                }
            }
        }

        private static void DecompressBlock(Object inputFileInfo)
        {
            FileInfo inputFile = (FileInfo)inputFileInfo;
            Byte[] compressedBuffer = new Byte[BUFF_SIZE + 2];
            Byte[] buff;

            using (MemoryStream memStream = new MemoryStream())
            using (FileStream localInputStream = new FileStream(inputFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 8192))
            {
                Int64 currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;

                while (currentBlock < _blocks)
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
                                    gzStream.CopyTo(memStream);
                                }

                                Console.Write(".");

                                //The header of the next member may be in a previously inflated block (8Kb)
                                i = (localInputStream.Position - fragmentStart) - 8191;
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

                    Monitor.Enter(StreamLock);

                    while (currentBlock != _currentBlockWrited)
                    {
                        Monitor.Wait(StreamLock);
                    }

                    buff = memStream.ToArray();
                    _outputStream.Write(buff, 0, buff.Length);
                    _currentBlockWrited++;

                    Monitor.PulseAll(StreamLock);
                    Monitor.Exit(StreamLock);

                    memStream.SetLength(0);
                    currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;
                }
            }
        }
    }
}