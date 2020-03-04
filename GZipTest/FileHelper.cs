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
        private readonly IProcessor _producer;

        internal FileHelper(IProcessor producer)
        {
            _producer = producer;
        }

        internal void WriteDecompressToFile(Object outputFile)
        {
            try
            {
                Int64 blockNumber;
                using (FileStream outputFileStream = new FileStream(((FileInfo)outputFile).FullName, FileMode.Create, FileAccess.Write))
                {
                    while ((blockNumber = _producer.OutputQueue.Dequeue(out Byte[] block)) >= 0)
                    {
                        outputFileStream.Position = blockNumber * GZipper.BUFF_SIZE;
                        outputFileStream.Write(block, 0, block.Length);
                        Console.Write("w" + blockNumber);
                    }
                }
                Console.WriteLine("WriteDecompressToFile thread ended");
            }
            catch (Exception ex)
            {
                if (_producer != null)
                    _producer.InnerException = ex;
            }
        }

        internal void WriteCompressToFile(Object outputFile)
        {
            try
            {
                Int64 blockNumber;
                using (FileStream outputFileStream = new FileStream(((FileInfo)outputFile).FullName, FileMode.Create, FileAccess.Write))
                {
                    while ((blockNumber = _producer.OutputQueue.Dequeue(out Byte[] block)) >= 0)
                    {
                        outputFileStream.Write(BitConverter.GetBytes(block.Length), 0, 4);
                        outputFileStream.Write(BitConverter.GetBytes(blockNumber), 0, 8);
                        outputFileStream.Write(block, 0, block.Length);
                        Console.Write("w" + blockNumber);
                    }
                }
                Console.WriteLine("WriteCompressToFile thread ended");
            }
            catch (Exception ex)
            {
                if (_producer != null)
                    _producer.InnerException = ex;
            }
        }

        internal void ReadDecompressFromFile(Object inputFile)
        {
            try
            {
                Int64 blockNumber = 0;
                Int64 blocksCount = (Int64)Math.Ceiling((Double)((FileInfo)inputFile).Length / GZipper.BUFF_SIZE);
                Int32 bytesReaded;
                Byte[] block = new Byte[1024 * 1024];
                using (FileStream inputFileStream = new FileStream(((FileInfo)inputFile).FullName, FileMode.Open, FileAccess.Read))
                {
                    while (blockNumber < blocksCount)
                    {
                        inputFileStream.Position = blockNumber * (1024 * 1024);
                        bytesReaded = inputFileStream.Read(block, 0, block.Length);
                        if (bytesReaded < block.Length)
                        {
                            Byte[] finalBlock = new Byte[bytesReaded];
                            Array.Copy(block, finalBlock, finalBlock.Length);
                            _producer.InputQueue.Enqueue(finalBlock, blockNumber);
                        }
                        else
                        {
                            _producer.InputQueue.Enqueue(block, blockNumber);
                        }
                        Console.Write("r" + blockNumber);
                        blockNumber++;
                    }
                    _producer.InputQueue.Close();
                }
                Console.WriteLine("ReadDecompressFromFile thread ended");
            }
            catch (Exception ex)
            {
                if (_producer != null)
                    _producer.InnerException = ex;
            }
        }

        internal void ReadCompressFromFile(Object inputFile)
        {
            try
            {
                Int64 blockNumber;
                Int32 bytesReaded, blockLength;
                Byte[] block, header = new Byte[12];
                using (FileStream inputFileStream = new FileStream(((FileInfo)inputFile).FullName, FileMode.Open, FileAccess.Read))
                {
                    while (inputFileStream.Position < ((FileInfo)inputFile).Length)
                    {
                        bytesReaded = inputFileStream.Read(header, 0, header.Length);
                        if (bytesReaded != header.Length)
                            throw new EndOfStreamException("Unexpected end of file");

                        blockLength = BitConverter.ToInt32(header, 0);
                        blockNumber = BitConverter.ToInt64(header, 4);
                        block = new Byte[blockLength];

                        bytesReaded = inputFileStream.Read(block, 0, block.Length);
                        if (bytesReaded != blockLength)
                            throw new EndOfStreamException("Unexpected end of file");

                        if (!_producer.InputQueue.Enqueue(block, blockNumber))
                            break;
                        Console.Write("r" + blockNumber);
                    }
                    _producer.InputQueue.Close();
                }
                Console.WriteLine("ReadCompressFromFile thread ended");
            }
            catch (Exception ex)
            {
                if (_producer != null)
                    _producer.InnerException = ex;
            }
        }
    }
}
