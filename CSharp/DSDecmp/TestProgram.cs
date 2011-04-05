using System;
using System.Collections.Generic;
using System.Text;
using DSDecmp.Formats.Nitro;
using System.IO;

namespace DSDecmp
{
    class TestProgram
    {

        public static void Main2(string[] args)
        {
            new RLE().Compress("tests/rle/testdata.dat", "tests/rle/cmp/testdata.rle.dat");
            new RLE().Decompress("tests/rle/cmp/testdata.rle.dat", "tests/rle/dec/testdata.elr.dat");
            Console.WriteLine("Success?");
            Console.ReadLine();

        }

    }
}
