using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    internal class Compressor : IProcessor
    {
        private readonly Int32 _threadsNumber;
        private Int64 blocksCount;
        private Int64 blocksProcessed;
        private Exception _innerException;

        public GZipperBlockingQueue InputQueue { get; }
        public GZipperBlockingQueue OutputQueue { get; }

        public Exception InnerException
        {
            get => _innerException;
            set
            {
                Interlocked.Exchange(ref _innerException, value);
                InputQueue.Close();
                OutputQueue.Close();
            }
        }

        public Compressor(Int32 threadsNumber)
        {
            InputQueue = new GZipperBlockingQueue(threadsNumber);
            OutputQueue = new GZipperBlockingQueue(threadsNumber);
            _threadsNumber = threadsNumber;
        }

        public void Start(FileInfo inputFile, FileInfo outputFile)
        {
            blocksCount = (Int64)Math.Ceiling((Double)inputFile.Length / GZipper.BUFF_SIZE);
            Thread[] threads = new Thread[_threadsNumber];

            for (Int64 i = 0; i < _threadsNumber; i++)
            {
                threads[i] = new Thread(Compress);
                threads[i].Start();
            }

            FileHelper fileHelper = new FileHelper(this);
            Thread inputProduce = new Thread(fileHelper.ReadDecompressFromFile);
            Thread outputConsume = new Thread(fileHelper.WriteCompressToFile);
            inputProduce.Start(inputFile);
            outputConsume.Start(outputFile);

            inputProduce.Join();

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            OutputQueue.Close();
            outputConsume.Join();

            if (InnerException != null)
                throw InnerException;
        }

        private void Compress()
        {
            try
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    while (InputQueue.Dequeue(out Byte[] block, out Int64 blockNumber))
                    {
                        using (GZipStream gzStream = new GZipStream(memStream,
                                                        CompressionMode.Compress, true))
                        {
                            gzStream.Write(block, 0, block.Length);
                        }

                        if (!OutputQueue.Enqueue(memStream.ToArray(), blockNumber))
                            break;
                        memStream.SetLength(0);
                    }
                }
            }
            catch (Exception ex)
            {
                InnerException = ex;
            }
        }

        public void Report(Int32 value)
        {
            Double percent = (Double)Interlocked.Increment(ref blocksProcessed) / (Double)blocksCount;
            Console.CursorLeft -= 4;
            Console.Write($"{percent,4:P0}");
        }
    }
}