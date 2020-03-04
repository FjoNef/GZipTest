using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GZipTest
{
    class Compressor : IProcessor
    {
        private readonly FileHelper _fileHelper;
        private readonly Int32 _threadsNumber;
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
            _fileHelper = new FileHelper(this);
            _threadsNumber = threadsNumber;
        }

        public void Start(FileInfo inputFile, FileInfo outputFile)
        {
            Thread[] threads = new Thread[_threadsNumber];

            for (Int64 i = 0; i < _threadsNumber; i++)
            {
                threads[i] = new Thread(Compress);
                threads[i].Start();
            }
            Thread inputProduce = new Thread(_fileHelper.ReadDecompressFromFile);
            Thread outputConsume = new Thread(_fileHelper.WriteCompressToFile);
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
                while (InputQueue.Dequeue(out Byte[] block, out Int64 blockNumber))
                {
                    Byte[] buff;
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        using (GZipStream gzStream = new GZipStream(memStream,
                                                        CompressionMode.Compress, true))
                        {
                            gzStream.Write(block, 0, block.Length);
                        }
                        memStream.Position = 0;
                        using (MemoryStream memStreamOutput = new MemoryStream())
                        {
                            using (GZipStream gzStream = new GZipStream(memStream,
                                                            CompressionMode.Decompress, true))
                            {
                                gzStream.CopyTo(memStreamOutput);
                            }

                            buff = memStream.ToArray();
                        }
                    }


                    if (!OutputQueue.Enqueue(buff, blockNumber))
                        break;
                }

                Console.WriteLine("Compress thread ended");
            }
            catch (Exception ex)
            {
                InnerException = ex;
            }
        }
    }
}
