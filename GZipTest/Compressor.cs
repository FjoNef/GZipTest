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
    class Compressor
    {
        private readonly GZipperThreadSyncronizer _threadSync;
        private readonly FileHelper _fileHelper;
        private Thread _inputProduce;
        private Thread _outputConsume;

        internal Exception InnerException { get; set; }

        internal Compressor(FileInfo inputFile, FileInfo outputFile, Int32 queueMaxLength)
        {
            _threadSync = new GZipperThreadSyncronizer(queueMaxLength);
            _fileHelper = new FileHelper(_threadSync, inputFile, outputFile);
            _inputProduce = new Thread(_fileHelper.ReadDecompressFromFile);
            _outputConsume = new Thread(_fileHelper.WriteCompressToFile);
        }

        internal void Start()
        {
            _inputProduce.Start();
            _outputConsume.Start();
        }

        internal void Compress()
        {
            try
            {
                Int64 blockNumber;
                using (MemoryStream memStream = new MemoryStream())
                {
                    while ((blockNumber = _threadSync.GetBlockFromInputQueue(out Byte[] block)) >= 0)
                    {
                        using (GZipStream gzStream = new GZipStream(memStream,
                                                        CompressionMode.Compress, true))
                        {
                            gzStream.Write(block, 0, block.Length);
                        }
                        if (!_threadSync.PutBlockToOutputQueue(memStream.ToArray(), blockNumber))
                            break;
                        memStream.SetLength(0);
                    }
                }
                Console.WriteLine("Compress thread ended");
            }
            catch (Exception ex)
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
