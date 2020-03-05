using System;
using System.IO;

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
                using (FileStream outputFileStream = new FileStream(((FileInfo)outputFile).FullName, FileMode.Create, FileAccess.Write))
                {
                    while (_producer.OutputQueue.Dequeue(out Byte[] block, out Int64 blockNumber))
                    {
                        outputFileStream.Position = blockNumber * GZipper.BUFF_SIZE;
                        outputFileStream.Write(block, 0, block.Length);
                    }
                }
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
                using (FileStream outputFileStream = new FileStream(((FileInfo)outputFile).FullName, FileMode.Create, FileAccess.Write))
                {
                    while (_producer.OutputQueue.Dequeue(out Byte[] block, out Int64 blockNumber))
                    {
                        outputFileStream.Write(BitConverter.GetBytes(block.Length), 0, 4);
                        outputFileStream.Write(BitConverter.GetBytes(blockNumber), 0, 8);
                        outputFileStream.Write(block, 0, block.Length);
                        _producer?.Report(1);
                    }
                }
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

                using (FileStream inputFileStream = new FileStream(((FileInfo)inputFile).FullName, FileMode.Open, FileAccess.Read))
                {
                    while (blockNumber < blocksCount)
                    {
                        Byte[] block = new Byte[GZipper.BUFF_SIZE];
                        inputFileStream.Position = blockNumber * GZipper.BUFF_SIZE;
                        bytesReaded = inputFileStream.Read(block, 0, block.Length);
                        if (bytesReaded < block.Length)
                        {
                            Byte[] finalBlock = new Byte[bytesReaded];
                            Array.Copy(block, finalBlock, finalBlock.Length);
                            if (!_producer.InputQueue.Enqueue(finalBlock, blockNumber))
                                break;
                        }
                        else
                        {
                            if (!_producer.InputQueue.Enqueue(block, blockNumber))
                                break;
                        }
                        blockNumber++;
                    }
                    _producer.InputQueue.Close();
                }
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
                Byte[] header = new Byte[12];
                using (FileStream inputFileStream = new FileStream(((FileInfo)inputFile).FullName, FileMode.Open, FileAccess.Read))
                {
                    while (inputFileStream.Position < ((FileInfo)inputFile).Length)
                    {
                        bytesReaded = inputFileStream.Read(header, 0, header.Length);
                        if (bytesReaded != header.Length)
                            throw new EndOfStreamException("Unexpected end of file");

                        blockLength = BitConverter.ToInt32(header, 0);
                        blockNumber = BitConverter.ToInt64(header, 4);
                        Byte[] block = new Byte[blockLength];

                        bytesReaded = inputFileStream.Read(block, 0, block.Length);
                        if (bytesReaded != blockLength)
                            throw new EndOfStreamException("Unexpected end of file");

                        if (!_producer.InputQueue.Enqueue(block, blockNumber))
                            break;
                        _producer.Report((Int32)(((Double)inputFileStream.Position / ((FileInfo)inputFile).Length) * 100));
                    }
                    _producer.InputQueue.Close();
                }
            }
            catch (Exception ex)
            {
                if (_producer != null)
                    _producer.InnerException = ex;
            }
        }
    }
}