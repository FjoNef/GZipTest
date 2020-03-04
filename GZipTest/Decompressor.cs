﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GZipTest
{
    internal class Decompressor : IProcessor
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

        public Decompressor(Int32 threadsNumber)
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
                threads[i] = new Thread(Decompress);
                threads[i].Start();
            }
            Thread inputProduce = new Thread(_fileHelper.ReadCompressFromFile);
            Thread outputConsume = new Thread(_fileHelper.WriteDecompressToFile);
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

        private void Decompress()
        {
            try
            {
                Int64 blockNumber;
                using (MemoryStream memStreamOutput = new MemoryStream())
                {
                    while ((blockNumber = InputQueue.Dequeue(out Byte[] block)) >= 0)
                    {
                        using (MemoryStream memStreamInput = new MemoryStream(block))
                        using (GZipStream gzStream = new GZipStream(memStreamInput,
                                                        CompressionMode.Decompress, false))
                        {
                            gzStream.CopyTo(memStreamOutput);
                        }
                        if (!OutputQueue.Enqueue(memStreamOutput.ToArray(), blockNumber))
                            break;

                        memStreamOutput.SetLength(0);
                    }
                }
                Console.WriteLine("Decompress thread ended");
            }
            catch (Exception ex)
            {
                InnerException = ex;
            }
        }
    }
}
