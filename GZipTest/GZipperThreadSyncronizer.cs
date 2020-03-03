using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GZipTest
{
    internal class GZipperThreadSyncronizer
    {
        private readonly Object InputQueueLock = new Object();
        private readonly Object OutputQueueLock = new Object();
        private Queue<KeyValuePair<Int64, Byte[]>> inputQueue = new Queue<KeyValuePair<Int64, Byte[]>>();
        private Queue<KeyValuePair<Int64, Byte[]>> outputQueue = new Queue<KeyValuePair<Int64, Byte[]>>();
        private bool isEOFReached;
        private bool isAllBlocksProcessed;

        internal GZipperThreadSyncronizer()
        {
            
        }

        internal Int64 GetBlockFromOutputQueue(out Byte[] block)
        {
            try
            {
                Monitor.Enter(OutputQueueLock);
                while (outputQueue.Count == 0)
                {
                    if (isAllBlocksProcessed)
                    {
                        block = new byte[0];
                        return -1;
                    }
                    Monitor.Wait(OutputQueueLock);
                }
                KeyValuePair<Int64, Byte[]> kvPair = outputQueue.Dequeue();
                block = kvPair.Value;
                return kvPair.Key;
            }
            finally
            {
                Monitor.Exit(OutputQueueLock);
            }
        }

        internal void PutBlockToOutputQueue(Byte[] block, Int64 blockNumber)
        {
            try
            {
                Monitor.Enter(OutputQueueLock);
                outputQueue.Enqueue(new KeyValuePair<Int64, Byte[]>(blockNumber, block));
                Monitor.Pulse(OutputQueueLock);
            }
            finally
            {
                Monitor.Exit(OutputQueueLock);
            }
        }

        internal void PutBlockToInputQueue(Byte[] block, Int64 blockNumber)
        {
            try
            {
                Monitor.Enter(InputQueueLock);
                inputQueue.Enqueue(new KeyValuePair<Int64, Byte[]>(blockNumber, block));
                Monitor.Pulse(InputQueueLock);
            }
            finally
            {
                Monitor.Exit(InputQueueLock);
            }
        }

        internal Int64 GetBlockFromInputQueue(out Byte[] block)
        {
            try
            {
                Monitor.Enter(InputQueueLock);
                while (inputQueue.Count == 0)
                {
                    if (isEOFReached)
                    {
                        block = new byte[0];
                        return -1;
                    }
                    Monitor.Wait(InputQueueLock);
                }
                KeyValuePair<Int64, Byte[]> kvPair = inputQueue.Dequeue();
                block = kvPair.Value;
                return kvPair.Key;
            }
            finally
            {
                Monitor.Exit(InputQueueLock);
            }
        }

        internal void SetEndOfFile()
        {
            Monitor.Enter(InputQueueLock);
            isEOFReached=true;
            Monitor.PulseAll(InputQueueLock);
            Monitor.Exit(InputQueueLock);
        }

        internal void SetAllBlocksProcessed()
        {
            Monitor.Enter(OutputQueueLock);
            isAllBlocksProcessed = true;
            Monitor.PulseAll(OutputQueueLock);
            Monitor.Exit(OutputQueueLock);
        }

        //private void UpdateProgress()
        //{
        //    Double percent = (Double)_currentBlockWrited / (Double)Blocks;
        //    Console.CursorLeft -= 4;
        //    Console.Write($"{percent,4:P0}");
        //}
    }
}
