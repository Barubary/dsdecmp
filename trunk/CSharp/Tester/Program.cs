using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameFormats;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start");

            //new LuminousArc().Compress("D:/tile molester/DSLazy/NDS_UNPACK_LUMARC/test/atcbg_dec.imb", "D:/tile molester/DSLazy/NDS_UNPACK_LUMARC/test/atcbg_dec_cmp.imb");
            //new LuminousArc().Decompress("D:/tile molester/DSLazy/NDS_UNPACK_LUMARC/test/atcbg_dec_cmp.imb", "D:/tile molester/DSLazy/NDS_UNPACK_LUMARC/test/atcbg_dec_cmp_dec.imb");

            new LuminousArc().Compress("D:/tile molester/DSLazy/NDS_UNPACK_LUMARC/test/o_lmoji00_dec.bin", "D:/tile molester/DSLazy/NDS_UNPACK_LUMARC/test/o_lmoji00_dec_cmp.bin");
            Console.WriteLine();
            new LuminousArc().Decompress("D:/tile molester/DSLazy/NDS_UNPACK_LUMARC/test/o_lmoji00_dec_cmp.bin", "D:/tile molester/DSLazy/NDS_UNPACK_LUMARC/test/o_lmoji00_dec_cmp_dec.bin");
            

            Console.WriteLine("Success?");
            Console.ReadLine();
        }
    }
}
