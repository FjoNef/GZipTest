using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    static class GZipper
    {
        private static readonly Int32 BUFF_SIZE = 1024 * 1024;
        private static readonly Int32 PROC_COUNT = Environment.ProcessorCount;
        private static readonly Object StreamLock = new Object();
        private static readonly Object QueueLock = new Object();

        private static Int64 _currentBlockReaded;
        private static Int64 _currentBlockWrited;
        private static Int64 _currentPosition;
        private static Boolean _isSearchCompleted;
        private static Int64 _blocks;
        private static FileStream _outputStream;
        private static Queue<Int64> _headerPositions = new Queue<long>();

        public static void Compress(String inputFileName, String outputFileName = null)
        {
            try
            {
                Console.Write("Compressing");

                FileInfo inputFile = new FileInfo(inputFileName);

                _currentBlockWrited = 0;
                _currentBlockReaded = 0;
                _currentPosition = 0;

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
                    _currentPosition = 0;

                    using (_outputStream = File.Create((outputFileName ?? inputFile.FullName.Replace(".gz", ""))))
                    {
                        _blocks = (Int64)Math.Ceiling((Double)inputFile.Length / BUFF_SIZE);

                        Int32 threadsNumber = (Int32)Math.Min(PROC_COUNT, _blocks);

                        Thread[] threads = new Thread[threadsNumber];
                        //threads[0] = new Thread(FindCompressedBlocks);
                        //threads[0].Start(inputFile);

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
            Byte[] buff;

            using (FileStream localInputStream = inputFile.OpenRead())
            {
                Int64 currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;

                while (currentBlock <= _blocks)
                {
                    Int64 fragmentStart = BUFF_SIZE * currentBlock;

                    localInputStream.Position = fragmentStart;
                    Int64 currentPosition = Interlocked.Read(ref _currentPosition);
                    if (currentPosition > localInputStream.Position)
                        localInputStream.Position = currentPosition;

                    Byte[] fragmentStartBytes = new Byte[3];

                    //2 additional bytes in case of "magic numbers" separation
                    while (localInputStream.Position < fragmentStart + BUFF_SIZE + 2)
                    {
                        Int32 bytesReaded = localInputStream.Read(fragmentStartBytes, 0, 3);

                        if (bytesReaded < 3)
                            break;

                        if (fragmentStartBytes[0] == 0x1F && fragmentStartBytes[1] == 0x8B && fragmentStartBytes[2] == 0x8)
                        {
                            Int64 previousPosition = localInputStream.Position;
                            localInputStream.Position -= 3;

                            try
                            {
                                using (MemoryStream memStream = new MemoryStream())
                                {
                                    using (GZipStream gzStream = new GZipStream(localInputStream,
                                                CompressionMode.Decompress, true))
                                    {
                                        gzStream.CopyTo(memStream);
                                    }
                                    buff = memStream.ToArray();
                                }

                                Monitor.Enter(StreamLock);
                                while (currentBlock != _currentBlockWrited)
                                {
                                    Monitor.Wait(StreamLock);
                                }

                                _outputStream.Write(buff, 0, buff.Length);

                                Monitor.PulseAll(StreamLock);

                                Monitor.Exit(StreamLock);

                                if (localInputStream.Position - 8192 >= fragmentStart + BUFF_SIZE + 2)
                                {
                                    Interlocked.Exchange(ref _currentPosition, localInputStream.Position - 8192);
                                    break;
                                }

                                //The header of the next member may be in a previously inflated block (8Kb)
                                if (localInputStream.Position > previousPosition + 8192)
                                    localInputStream.Position -= 8192;

                                Console.Write(".");
                            }
                            catch (InvalidDataException)
                            {
                                localInputStream.Position = previousPosition;
                            }
                        }
                        else
                        {
                            currentPosition = Interlocked.Read(ref _currentPosition);
                            if (currentPosition > localInputStream.Position)
                                localInputStream.Position = currentPosition;
                            else
                                localInputStream.Position -= 2;
                        }
                    }

                    Monitor.Enter(StreamLock);

                    while (currentBlock != _currentBlockWrited)
                    {
                        Monitor.Wait(StreamLock);
                    }
                    _currentBlockWrited++;

                    Monitor.PulseAll(StreamLock);
                    Monitor.Exit(StreamLock);

                    currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;
                }
            }
        }

        //private static void DecompressBlock(Object inputFileInfo)
        //{
        //    FileInfo inputFile = (FileInfo)inputFileInfo;
        //    Byte[] decompressedBuff;
        //    Int64 currentBlock = 0;

        //    using (FileStream localInputStream = inputFile.OpenRead())
        //    {
        //        while (true)
        //        {

        //            Monitor.Enter(QueueLock);

        //            while (_headerPositions.Count == 0)
        //            {
        //                if (Volatile.Read(ref _isSearchCompleted))
        //                {
        //                    Monitor.Exit(QueueLock);
        //                    return;
        //                }
        //                Monitor.Wait(QueueLock);
        //            }

        //            Int64 currentPosition = _headerPositions.Dequeue();
        //            currentBlock = _currentBlockReaded++;

        //            Monitor.Exit(QueueLock);

        //            if (currentPosition >= Interlocked.Read(ref _currentPosition))
        //            {
        //                localInputStream.Position = currentPosition;
        //                try
        //                {
        //                    using (MemoryStream memStream = new MemoryStream())
        //                    {
        //                        using (GZipStream gzStream = new GZipStream(localInputStream,
        //                                    CompressionMode.Decompress, true))
        //                        {
        //                            gzStream.CopyTo(memStream);
        //                        }
        //                        decompressedBuff = memStream.ToArray();
        //                    }


        //                    Interlocked.Exchange(ref _currentPosition, localInputStream.Position - 8192);

        //                    Monitor.Enter(StreamLock);

        //                    while (currentBlock != _currentBlockWrited)
        //                        Monitor.Wait(StreamLock);


        //                    _outputStream.Write(decompressedBuff, 0, decompressedBuff.Length);
        //                    _currentBlockWrited++;

        //                    Monitor.PulseAll(StreamLock);

        //                    Monitor.Exit(StreamLock);

        //                    Console.Write(".");
        //                }
        //                catch (InvalidDataException)
        //                {
        //                    Interlocked.Increment(ref _currentBlockWrited);
        //                }
        //            }
        //            else
        //            {
        //                Interlocked.Increment(ref _currentBlockWrited);
        //            }
        //        }
        //    }
        //}

        private static void FindCompressedBlocks(Object inputFileInfo)
        {
            FileInfo inputFile = (FileInfo)inputFileInfo;
            Byte[] fragmentStartBytes = new Byte[3];
            using (FileStream localInputStream = inputFile.OpenRead())
            {
                while (localInputStream.Position < localInputStream.Length)
                {
                    Int32 bytesReaded = localInputStream.Read(fragmentStartBytes, 0, 3);

                    if (bytesReaded < 3)
                        break;

                    if (fragmentStartBytes[0] == 0x1F && fragmentStartBytes[1] == 0x8B && fragmentStartBytes[2] == 0x8)
                    {
                        Monitor.Enter(QueueLock);
                        _headerPositions.Enqueue(localInputStream.Position - 3);
                        Monitor.Pulse(QueueLock);
                        Monitor.Exit(QueueLock);
                    }
                    else
                    {
                        localInputStream.Position -= 2;
                    }

                    Int64 currentPosition = Interlocked.Read(ref _currentPosition);
                    if (currentPosition > localInputStream.Position)
                        localInputStream.Position = currentPosition;
                }

                Volatile.Write(ref _isSearchCompleted, true);
            }
        }
    }
}
