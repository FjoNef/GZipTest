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
    internal class Decompressor
    {
        private readonly GZipperThreadSyncronizer _threadSync;
        private readonly FileHelper _fileHelper;
        private Thread _inputProduce;
        private Thread _outputConsume;

        internal Exception InnerException { get; set; }

        internal Decompressor(FileInfo inputFile, FileInfo outputFile, Int32 queueMaxLength)
        {
            _threadSync = new GZipperThreadSyncronizer(queueMaxLength);
            _fileHelper = new FileHelper(_threadSync, inputFile, outputFile);
            _inputProduce = new Thread(_fileHelper.ReadCompressFromFile);
            _outputConsume = new Thread(_fileHelper.WriteDecompressToFile);
        }

        internal void Start()
        {
            _inputProduce.Start();
            _outputConsume.Start();
        }

        internal void Decompress()
        {
            try
            {
                Int64 blockNumber;
                using (MemoryStream memStreamOutput = new MemoryStream())
                {
                    while ((blockNumber = _threadSync.GetBlockFromInputQueue(out Byte[] block)) >= 0)
                    {
                        using (MemoryStream memStreamInput = new MemoryStream(block))
                        using (GZipStream gzStream = new GZipStream(memStreamInput,
                                                        CompressionMode.Decompress, false))
                        {
                            gzStream.CopyTo(memStreamOutput);
                        }
                        if (!_threadSync.PutBlockToOutputQueue(memStreamOutput.ToArray(), blockNumber))
                            break;

                        memStreamOutput.SetLength(0);
                    }
                }
                Console.WriteLine("Decompress thread ended");
            }
            catch(Exception ex)
            {
                _threadSync.SetEndOfFile();
                _threadSync.SetAllBlocksProcessed();
                InnerException = ex;
            }
        }

        internal void Finish()
        {
            _inputProduce.Join();
            if (_fileHelper.InnerException != null)
                throw _fileHelper.InnerException;
            _threadSync.SetAllBlocksProcessed();
            _outputConsume.Join();
            if (_fileHelper.InnerException != null)
                throw _fileHelper.InnerException;
        }
    }
}
