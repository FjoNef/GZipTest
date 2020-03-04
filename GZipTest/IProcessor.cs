using System;
using System.IO;

namespace GZipTest
{
    internal interface IProcessor
    {
        GZipperBlockingQueue InputQueue { get; }
        GZipperBlockingQueue OutputQueue { get; }
        Exception InnerException { get; set; }

        void Start(FileInfo inputFile, FileInfo outputFile);
    }
}