using System;
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
        private static readonly AutoResetEvent are = new AutoResetEvent(false);

        private static Int64 _currentBlockReaded;
        private static Int64 _currentBlockWrited;
        private static Int64 _currentPosition;
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
            Int64 currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;

            FileInfo inputFile = (FileInfo)inputFileInfo;

            using (FileStream localInputStream = inputFile.OpenRead())
            {
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

            using (FileStream localInputStream = inputFile.OpenRead())
            {
                Int64 currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;

                while (currentBlock <= _blocks)
                {
                    Int64 fragmentStart = BUFF_SIZE * currentBlock;

                    localInputStream.Position = fragmentStart;
                    var currentPosition = Interlocked.Read(ref _currentPosition);
                    if (currentPosition > localInputStream.Position)
                        localInputStream.Position = currentPosition;

                    Byte[] fragmentStartBytes = new Byte[3];

                    //2 additional bytes in case of "magic numbers" separation
                    while (localInputStream.Position < fragmentStart + BUFF_SIZE + 2)
                    {
                        var bytesReaded = localInputStream.Read(fragmentStartBytes, 0, 3);

                        if (bytesReaded < 3)
                            break;

                        if (fragmentStartBytes[0] == 31 && fragmentStartBytes[1] == 139 && fragmentStartBytes[2] == 8)
                        {
                            var previousPosition = localInputStream.Position;
                            localInputStream.Position -= 3;

                            Monitor.Enter(StreamLock);
                            while (currentBlock != _currentBlockWrited)
                            {
                                Monitor.Wait(StreamLock);
                            }

                            try
                            {
                                using (GZipStream gzStream = new GZipStream(localInputStream,
                                    CompressionMode.Decompress, true))
                                {
                                    lock (StreamLock)
                                    {
                                        gzStream.CopyTo(_outputStream);
                                    }
                                }

                                Console.Write(".");
                            }
                            catch (InvalidDataException)
                            {
                                localInputStream.Position = previousPosition;
                            }

                            Monitor.PulseAll(StreamLock);

                            Monitor.Exit(StreamLock);

                            if (localInputStream.Position - 8192 >= fragmentStart + BUFF_SIZE + 2)
                            {
                                Interlocked.Exchange(ref _currentPosition, localInputStream.Position - 8192);
                                break;
                            }

                            //The header of the next member may be in a previously inflated block (8Kb)
                            if (localInputStream.Position != previousPosition)
                                localInputStream.Position -= 8192;
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

                    while (true)
                    {
                        if (currentBlock == _currentBlockWrited)
                        {
                            Interlocked.Increment(ref _currentBlockWrited);
                            break;
                        }
                    }

                    currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;
                }
            }
        }
    }
}
