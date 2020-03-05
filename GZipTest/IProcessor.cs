using System;
using System.IO;

namespace GZipTest
{
    internal interface IProcessor : IProgress<Int32>
    {
        GZipperBlockingQueue InputQueue { get; }
        GZipperBlockingQueue OutputQueue { get; }
        Exception InnerException { get; set; }

        void Start(FileInfo inputFile, FileInfo outputFile);
    }
}