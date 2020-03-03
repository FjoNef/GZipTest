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

        internal Compressor(FileInfo inputFile, FileInfo outputFile)
        {
            _threadSync = new GZipperThreadSyncronizer();
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
            Int64 blockNumber;
            while ((blockNumber = _threadSync.GetBlockFromInputQueue(out Byte[] block)) >= 0)
            {
                using (MemoryStream memStream = new MemoryStream())
                using (GZipStream gzStream = new GZipStream(memStream,
                                                CompressionMode.Compress, true))
                {
                    gzStream.Write(block, 0, block.Length);
                    _threadSync.PutBlockToOutputQueue(memStream.ToArray(), blockNumber);
                }
            }
        }

        internal void Finish()
        {
            _inputProduce.Join();
            _threadSync.SetAllBlocksProcessed();
            _outputConsume.Join();
        }
    }
}
