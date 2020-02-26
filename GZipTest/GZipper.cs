using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    public class GZipper
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

        private readonly Object StreamLock = new Object();

        private Int64 _currentBlockReaded = 0;
        private Int64 _currentBlockWrited = 0;
        private Int64 _blocks = 0;
        private FileStream _outputStream = null;

        public void Compress(String inputFileName, String outputFileName = null)
        {
            try
            {
                Console.Write("Compressing...   0%");

                FileInfo inputFile = new FileInfo(inputFileName);

                _currentBlockWrited = 0;
                _currentBlockReaded = 0;

                using (_outputStream = new FileStream((outputFileName ?? inputFile.FullName) + ".gz", FileMode.Create, FileAccess.Write))
                {
                    _blocks = (Int64)Math.Ceiling((Double)inputFile.Length / BUFF_SIZE);

                    if (_blocks > 0)
                    {
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
                    else
                    {
                        CompressEmptyFile(inputFile);
                    }
                }
                Console.WriteLine("\nCompleted");
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("\nNo such file or directory");
            }
        }

        private void CompressEmptyFile(FileInfo inputFile)
        {
            _outputStream.Write(_headerBytes, 0, _headerBytes.Length);
            _outputStream.Write(new byte[8], 0, 8);
        }

        public void Decompress(String inputFileName, String outputFileName = null)
        {
            try
            {
                FileInfo inputFile = new FileInfo(inputFileName);

                if (inputFile.Extension == ".gz")
                {
                    Console.Write("Decompressing...   0%");

                    _currentBlockWrited = 0;
                    _currentBlockReaded = 0;

                    using (_outputStream = File.Create((outputFileName ?? inputFile.FullName.Replace(".gz", ""))))
                    {
                        _blocks = (Int64)Math.Ceiling((Double)inputFile.Length / BUFF_SIZE);

                        _blocks = _blocks > 0 ? _blocks : 1;

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
                    Console.WriteLine("\nCompleted");
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

        private void CompressBlock(Object inputFileInfo)
        {
            Int32 bytesReaded;
            Byte[] decompressedBuff = new Byte[BUFF_SIZE];

            FileInfo inputFile = (FileInfo)inputFileInfo;

            using (MemoryStream memStream = new MemoryStream())
            using (FileStream localInputStream = new FileStream(inputFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, DEFLATE_BUFF_SIZE))
            {
                Int64 currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;

                while (currentBlock < _blocks)
                {
                    Int64 fragmentStart = BUFF_SIZE * currentBlock;

                    localInputStream.Position = fragmentStart;

                    bytesReaded = localInputStream.Read(decompressedBuff, 0, decompressedBuff.Length);

                    using (GZipStream gzStream = new GZipStream(memStream, CompressionMode.Compress, true))
                    {
                        gzStream.Write(decompressedBuff, 0, bytesReaded);
                    }

                    WriteToFile(memStream, currentBlock, true);

                    currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;
                }
            }
        }

        private void DecompressBlock(Object inputFileInfo)
        {
            FileInfo inputFile = (FileInfo)inputFileInfo;
            Byte[] compressedBuffer = new Byte[BUFF_SIZE + 2];

            using (MemoryStream memStream = new MemoryStream())
            using (FileStream localInputStream = new FileStream(inputFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, DEFLATE_BUFF_SIZE))
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
                                    byte[] buffer = new byte[DEFLATE_BUFF_SIZE*10];
                                    int read;
                                    while ((read = gzStream.Read(buffer, 0, buffer.Length)) != 0)
                                    {
                                        memStream.Write(buffer, 0, read);
                                        if (memStream.Length > BUFF_SIZE * 10)
                                            WriteToFile(memStream, currentBlock, false);
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

                    WriteToFile(memStream, currentBlock, true);
                    currentBlock = Interlocked.Increment(ref _currentBlockReaded) - 1;
                }
            }
        }

        private void WriteToFile(MemoryStream memStream, Int64 currentBlock, Boolean isLastBlock)
        {
            Byte[] decompressedBuff = memStream.ToArray();

            Monitor.Enter(StreamLock);

            while (currentBlock != _currentBlockWrited)
            {
                Monitor.Wait(StreamLock);
            }

            _outputStream.Write(decompressedBuff, 0, decompressedBuff.Length);

            if (isLastBlock)
            {
                _currentBlockWrited++;

                UpdateProgress();
            }

            Monitor.PulseAll(StreamLock);
            Monitor.Exit(StreamLock);

            memStream.SetLength(0);
        }

        private void UpdateProgress()
        {
            Double percent = (Double)_currentBlockWrited / (Double)_blocks;
            Console.CursorLeft -= 4;
            Console.Write($"{percent,4:P0}");
        }
    }
}