using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
