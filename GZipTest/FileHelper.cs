using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest
{
    internal class FileHelper
    {
        private GZipperStreamSyncronizer _streamSync;

        internal FileHelper(GZipperStreamSyncronizer gZipperStreamSyncronizer)
        {
            _streamSync = gZipperStreamSyncronizer;
        }

        internal void WriteDecompressToFile()
        {
            Int64 blockNumber;
            using (FileStream outputFile = new FileStream(_streamSync.OutputFile.FullName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write))
            {
                while ((blockNumber = _streamSync.GetDecompressedBlock(out Byte[] block)) > 0)
                {
                    outputFile.Position = blockNumber * (1024 * 1024);
                    outputFile.Write(block, 0, block.Length);
                }
            }
        }

        internal void WriteCompressToFile()
        {
            Int64 blockNumber;
            using (FileStream outputFile = new FileStream(_streamSync.OutputFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                while ((blockNumber = _streamSync.GetCompressedBlock(out Byte[] block)) > 0)
                {
                    outputFile.Write(block, 0, block.Length);
                }
            }
        }

        internal void ReadDecompressFromFile()
        {
            Int64 blockNumber = _streamSync.CurrentBlockReaded;
            Int32 bytesReaded;
            Byte[] block = new Byte[1024 * 1024];
            using (FileStream inputFile = new FileStream(_streamSync.InputFile.FullName,FileMode.Open,FileAccess.Read,FileShare.Read))
            {                
                while ((blockNumber = _streamSync.CurrentBlockReaded)>0)
                {
                    inputFile.Position = blockNumber * (1024 * 1024);
                    bytesReaded = inputFile.Read(block, 0, block.Length);
                    _streamSync.PutBlockToQueue(block, blockNumber);
                }
            }
        }

        internal void ReadCompressFromFile()
        {
            Int64 blockNumber = _streamSync.CurrentBlockReaded;
            Int32 bytesReaded;
            Byte[] block;
            using (FileStream inputFile = new FileStream(_streamSync.InputFile.FullName, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                while (inputFile.Position<inputFile.Length)
                {
                    inputFile.Position = blockNumber * (1024 * 1024);
                    bytesReaded = inputFile.Read(block, 0, block.Length);
                    _streamSync.PutBlockToQueue(block, blockNumber);
                }
            }
        }
    }
}
