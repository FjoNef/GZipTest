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

        internal Decompressor(FileInfo inputFile, FileInfo outputFile)
        {
            _threadSync = new GZipperThreadSyncronizer();
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
            Int64 blockNumber;

            while ((blockNumber = _threadSync.GetBlockFromInputQueue(out Byte[] block)) >= 0)
            {
                using (MemoryStream memStreamInput = new MemoryStream(block))
                using (MemoryStream memStreamOutput = new MemoryStream())
                using (GZipStream gzStream = new GZipStream(memStreamInput,
                                                CompressionMode.Decompress, true))
                {
                    gzStream.CopyTo(memStreamOutput);
                    _threadSync.PutBlockToOutputQueue(memStreamOutput.ToArray(), blockNumber);
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
