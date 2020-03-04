﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest
{
    internal class FileHelper
    {
        private readonly GZipperThreadSyncronizer _threadSync;
        private readonly FileInfo _inputFile;
        private readonly FileInfo _outputFile;

        internal Exception InnerException { get; set; }

        internal FileHelper(GZipperThreadSyncronizer gZipperStreamSyncronizer, FileInfo inputFile, FileInfo outputFile)
        {
            _threadSync = gZipperStreamSyncronizer;
            _inputFile = inputFile;
            _outputFile = outputFile;
        }

        internal void WriteDecompressToFile()
        {
            try
            {
                Int64 blockNumber;
                using (FileStream outputFile = new FileStream(_outputFile.FullName, FileMode.Create, FileAccess.Write))
                {
                    while ((blockNumber = _threadSync.GetBlockFromOutputQueue(out Byte[] block)) >= 0)
                    {
                        outputFile.Position = blockNumber * (1024 * 1024);
                        outputFile.Write(block, 0, block.Length);
                        Console.Write("w" + blockNumber);
                    }
                }
                Console.WriteLine("WriteDecompressToFile thread ended");
            }
            catch (Exception ex)
            {
                _threadSync.SetEndOfFile();
                _threadSync.SetAllBlocksProcessed();
                InnerException = ex;
            }
        }

        internal void WriteCompressToFile()
        {
            try
            {
                Int64 blockNumber;
                using (FileStream outputFile = new FileStream(_outputFile.FullName, FileMode.Create, FileAccess.Write))
                {
                    while ((blockNumber = _threadSync.GetBlockFromOutputQueue(out Byte[] block)) >= 0)
                    {
                        outputFile.Write(BitConverter.GetBytes(block.Length), 0, 4);
                        outputFile.Write(BitConverter.GetBytes(blockNumber), 0, 8);
                        outputFile.Write(block, 0, block.Length);
                        Console.Write("w" + blockNumber);
                    }
                }
                Console.WriteLine("WriteCompressToFile thread ended");
            }
            catch (Exception ex)
            {
                _threadSync.SetEndOfFile();
                _threadSync.SetAllBlocksProcessed();
                InnerException = ex;
            }
        }

        internal void ReadDecompressFromFile()
        {
            try
            {
                Int64 blockNumber = 0;
                Int64 blocksCount = (Int64)Math.Ceiling((Double)_inputFile.Length / (1024 * 1024));
                Int32 bytesReaded;
                Byte[] block = new Byte[1024 * 1024];
                using (FileStream inputFile = new FileStream(_inputFile.FullName, FileMode.Open, FileAccess.Read))
                {
                    while (blockNumber < blocksCount)
                    {
                        inputFile.Position = blockNumber * (1024 * 1024);
                        bytesReaded = inputFile.Read(block, 0, block.Length);
                        if (bytesReaded < block.Length)
                        {
                            Byte[] finalBlock = new Byte[bytesReaded];
                            Array.Copy(block, finalBlock, finalBlock.Length);
                            _threadSync.PutBlockToInputQueue(finalBlock, blockNumber);
                        }
                        else
                        {
                            _threadSync.PutBlockToInputQueue(block, blockNumber);
                        }
                        Console.Write("r" + blockNumber);
                        blockNumber++;
                    }
                    _threadSync.SetEndOfFile();
                }
                Console.WriteLine("ReadDecompressFromFile thread ended");
            }
            catch (Exception ex)
            {
                _threadSync.SetEndOfFile();
                _threadSync.SetAllBlocksProcessed();
                InnerException = ex;
            }
        }

        internal void ReadCompressFromFile()
        {
            try
            {
                Int64 blockNumber;
                Int32 bytesReaded, blockLength;
                Byte[] block, header = new Byte[12];
                using (FileStream inputFile = new FileStream(_inputFile.FullName, FileMode.Open, FileAccess.Read))
                {
                    while (inputFile.Position < inputFile.Length)
                    {
                        bytesReaded = inputFile.Read(header, 0, header.Length);
                        if (bytesReaded != header.Length)
                            throw new EndOfStreamException("Unexpected end of file");

                        blockLength = BitConverter.ToInt32(header, 0);
                        blockNumber = BitConverter.ToInt64(header, 4);
                        block = new Byte[blockLength];

                        bytesReaded = inputFile.Read(block, 0, block.Length);
                        if (bytesReaded != blockLength)
                            throw new EndOfStreamException("Unexpected end of file");

                        if (!_threadSync.PutBlockToInputQueue(block, blockNumber))
                            break;
                        Console.Write("r" + blockNumber);
                    }
                    _threadSync.SetEndOfFile();
                }
                Console.WriteLine("ReadCompressFromFile thread ended");
            }
            catch (Exception ex)
            {
                _threadSync.SetEndOfFile();
                _threadSync.SetAllBlocksProcessed();
                InnerException = ex;
            }
        }
    }
}
