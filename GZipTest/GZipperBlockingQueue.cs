using System;
using System.Collections.Generic;
using System.Threading;

namespace GZipTest
{
    internal class GZipperBlockingQueue
    {
        private readonly Object _queueLock = new Object();
        private readonly Int32 _maxLength;
        private Queue<Tuple<Int64, Byte[]>> inputQueue;
        private bool isClosed;

        internal GZipperBlockingQueue(Int32 maxLength = Int32.MaxValue)
        {
            inputQueue = new Queue<Tuple<Int64, Byte[]>>();
            _maxLength = maxLength;
        }

        internal bool Dequeue(out Byte[] block, out Int64 blockNumber)
        {
            Tuple<Int64, Byte[]> kvPair;
            lock (_queueLock)
            {
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
                kvPair = inputQueue.Dequeue();
            }
            block = kvPair.Item2;
            blockNumber = kvPair.Item1;
            return true;
        }

        internal bool Enqueue(Byte[] block, Int64 blockNumber)
        {
            lock (_queueLock)
            {
                while (inputQueue.Count >= _maxLength)
                {
                    if (isClosed)
                        return false;
                    Monitor.Pulse(_queueLock);
                    Monitor.Wait(_queueLock);
                }
                inputQueue.Enqueue(new Tuple<Int64, Byte[]>(blockNumber, block));
                Monitor.Pulse(_queueLock);
            }
            return true;
        }

        internal void Close()
        {
            lock (_queueLock)
            {
                isClosed = true;
                Monitor.PulseAll(_queueLock);
            }
        }
    }
}