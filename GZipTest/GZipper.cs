using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    static class GZipper
    {
        private static readonly int BUFF_SIZE = 1024 * 1024;
        private static readonly int PROC_COUNT = Environment.ProcessorCount;
        private static readonly Object StreamLock = new Object();

        private static long _currentBlockReaded;
        private static long _currentBlockWrited;
        private static long _currentPosition;
        private static long _blocks;
        private static FileStream _inputStream;
        private static FileStream _outputStream;

        public static void Compress(string inputFileName, string outputFileName = null)
        {
            try
            {
                Console.Write("Compressing");

                var inputFile = new FileInfo(inputFileName);

                _currentBlockWrited = 0;
                _currentBlockReaded = 0;
                _currentPosition = 0;

                using (_inputStream = inputFile.OpenRead())
                using (_outputStream = File.Create((outputFileName ?? inputFile.FullName) + ".gz"))
                {
                    _blocks = (long)Math.Ceiling((double)_inputStream.Length / BUFF_SIZE);

                    Thread[] threads = new Thread[PROC_COUNT];

                    for (long i = 0; i < PROC_COUNT; i++)
                    {
                        threads[i] = new Thread(CompressBlock);
                        threads[i].Start();
                    }

                    foreach (var thread in threads)
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

        public static void Decompress(string inputFileName, string outputFileName = null)
        {
            try
            {
                var inputFile = new FileInfo(inputFileName);

                if (inputFile.Extension == ".gz")
                {
                    Console.Write("Decompressing");

                    _currentBlockWrited = 0;
                    _currentBlockReaded = 0;
                    _currentPosition = 0;

                    using (_outputStream = File.Create((outputFileName ?? inputFile.FullName.Replace(".gz", ""))))
                    {
                        _blocks = (long)Math.Ceiling((double)inputFile.Length / BUFF_SIZE);

                        Thread[] threads = new Thread[PROC_COUNT];

                        for (long i = 0; i < PROC_COUNT; i++)
                        {
                            threads[i] = new Thread(DecompressBlock);
                            threads[i].Start(inputFile);
                        }

                        foreach (var thread in threads)
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

        private static void CompressBlock()
        {
            var buff = new byte[BUFF_SIZE];
            int bytesReaded;
            long currentBlock = 0;

            while (currentBlock <= _blocks)
            {

                lock (StreamLock)
                {
                    currentBlock = _currentBlockReaded;
                    _currentBlockReaded++;
                    bytesReaded = _inputStream.Read(buff, 0, buff.Length);
                }

                using (MemoryStream memStream = new MemoryStream())
                {
                    using (GZipStream gzStream = new GZipStream(memStream, CompressionMode.Compress, true))
                    {
                        gzStream.Write(buff, 0, bytesReaded);
                    }
                    buff = memStream.ToArray();
                }

                while (true)
                {
                    if (currentBlock == _currentBlockWrited)
                    {
                        lock (StreamLock)
                        {
                            _outputStream.Write(buff, 0, buff.Length);
                            _currentBlockWrited++;
                        }
                        break;
                    }
                }

                Console.Write(".");
            }
        }

        private static void DecompressBlock(Object inputFileInfo)
        {
            long currentBlock=0;

            FileInfo inputFile = (FileInfo)inputFileInfo;

            using (FileStream localInputStream = inputFile.OpenRead())
            {
                while (currentBlock <= _blocks)
                {
                    lock (StreamLock)
                    {
                        currentBlock = _currentBlockReaded;
                        _currentBlockReaded++;
                    }

                    long fragmentStart = BUFF_SIZE * currentBlock;

                    localInputStream.Position = fragmentStart;
                    var currentPosition = Interlocked.Read(ref _currentPosition);
                    if (currentPosition > localInputStream.Position)
                        localInputStream.Position = currentPosition;

                    byte[] fragmentStartBytes = new byte[3];

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

                            while (true)
                            {
                                if (currentBlock == _currentBlockWrited)
                                {
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
                                    break;
                                }
                            }

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
                }
            }
        }
    }
}
