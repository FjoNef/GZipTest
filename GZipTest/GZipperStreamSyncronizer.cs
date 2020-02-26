using System;
using System.IO;
using System.Threading;

namespace GZipTest
{
    internal class GZipperStreamSyncronizer : IDisposable
    {
        private readonly Object StreamLock = new Object();
        private FileStream _outputStream = null;
        private Int64 _currentBlockReaded = 0;
        private Int64 _currentBlockWrited = 0;

        internal FileInfo InputFile { get; }

        internal Int64 CurrentBlockReaded { get => Interlocked.Increment(ref _currentBlockReaded) - 1; }

        internal Int64 Blocks { get; set; }

        internal GZipperStreamSyncronizer(FileInfo inputFile, String outputFileName)
        {
            InputFile = inputFile;
            _outputStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write);
        }

        internal void WriteBlockToFile(MemoryStream memStream, Int64 currentBlock, Boolean isLastChunk)
        {
            Byte[] decompressedBuff = memStream.ToArray();

            Monitor.Enter(StreamLock);

            while (currentBlock != _currentBlockWrited)
            {
                Monitor.Wait(StreamLock);
            }

            _outputStream.Write(decompressedBuff, 0, decompressedBuff.Length);

            if (isLastChunk)
            {
                _currentBlockWrited++;

                UpdateProgress();
            }

            Monitor.PulseAll(StreamLock);
            Monitor.Exit(StreamLock);

            memStream.SetLength(0);
        }

        internal void WriteToFile(byte[] buff, int offset, int count)
        {
            lock (StreamLock)
            {
                _outputStream.Write(buff, offset, count);
            }
        }

        private void UpdateProgress()
        {
            Double percent = (Double)_currentBlockWrited / (Double)Blocks;
            Console.CursorLeft -= 4;
            Console.Write($"{percent,4:P0}");
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_outputStream != null)
                    {
                        _outputStream.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
