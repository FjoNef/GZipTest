using System;
using System.Collections.Generic;
using System.Threading;

namespace GZipTest
{
    internal class GZipperBlockingQueue
    {
        private readonly Object _queueLock = new Object();
        private readonly Int32 _maxLength;
        private Queue<KeyValuePair<Int64, Byte[]>> inputQueue;
        private bool isClosed;

        internal GZipperBlockingQueue(Int32 maxLength = Int32.MaxValue)
        {
            inputQueue = new Queue<KeyValuePair<Int64, Byte[]>>();
            _maxLength = maxLength;
        }

        internal bool Dequeue(out Byte[] block, out Int64 blockNumber)
        {
            try
            {
                Monitor.Enter(_queueLock);
                while (inputQueue.Count == 0)
                {
                    if (isClosed)
                    {
                        block = new byte[0];
                        blockNumber = -1;
                        return false;
                    }
                    Monitor.Pulse(_queueLock);
                    Monitor.Wait(_queueLock);
                }
                KeyValuePair<Int64, Byte[]> kvPair = inputQueue.Dequeue();
                block = kvPair.Value;
                blockNumber = kvPair.Key;
                return true;
            }
            finally
            {
                Monitor.Exit(_queueLock);
            }
        }

        internal bool Enqueue(Byte[] block, Int64 blockNumber)
        {
            try
            {
                Monitor.Enter(_queueLock);
                while (inputQueue.Count >= _maxLength)
                {
                    if (isClosed)
                        return false;
                    Monitor.Pulse(_queueLock);
                    Monitor.Wait(_queueLock);
                }
                inputQueue.Enqueue(new KeyValuePair<Int64, Byte[]>(blockNumber, block));
                Monitor.Pulse(_queueLock);
                return true;
            }
            finally
            {
                Monitor.Exit(_queueLock);
            }
        }

        internal void Close()
        {
            Monitor.Enter(_queueLock);
            isClosed = true;
            Monitor.PulseAll(_queueLock);
            Monitor.Exit(_queueLock);
        }

        //private void UpdateProgress()
        //{
        //    Double percent = (Double)_currentBlockWrited / (Double)Blocks;
        //    Console.CursorLeft -= 4;
        //    Console.Write($"{percent,4:P0}");
        //}
    }
}