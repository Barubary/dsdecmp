using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DSDecmp
{
    class TestProgram
    {

        public static void MainTest(string[] args)
        {
            /*
            new RLE().Compress("tests/rle/testdata.dat", "tests/rle/cmp/testdata.rle.dat");
            new RLE().Decompress("tests/rle/cmp/testdata.rle.dat", "tests/rle/dec/testdata.elr.dat");
            /**/
            
            //Program.Main1(new string[] { "tests/huff/00.dat", "tests/huff/dec2/" });
            //Console.WriteLine("-----------------------------------------------------");
            //new Huffman().Decompress("tests/huff/00.dat", "tests/huff/dec/00.ffuh.dat");
            /**/
            //new LZ11().Decompress("tests/lz11/game_over_NCGR.cdat", "tests/lz11/dec/game_over.11zl.NCGR");

            //new LZOvl().Decompress("tests/lzovl/overlay_0001.bin", "tests/lzovl/dec/overlay_0001.dat");

            //new LZ10().Decompress("tests/lz10/npc002_LZ.bin", "tests/lz10/dec/npc002.narc");
            //LZ10.LookAhead = true;
            //new LZ10().Compress("tests/lz10/dec/npc002.narc", "tests/lz10/cmp/npc002_d.narc.lz");
            //new LZ10().Decompress("tests/lz10/cmp/npc002_d.narc.lz", "tests/lz10/cmpdec/npc002.narc");

            //LZ11.LookAhead = true;
            //new LZ11().Compress("tests/lz11/dec/game_over.11zl.NCGR", "tests/lz11/cmp/game_over.NCGR2.lz11");
            //new LZ11().Decompress("tests/lz11/cmp/game_over.NCGR2.lz11", "tests/lz11/cmpdec/game_over.NCGR");

            //LZOvl.LookAhead = true;
            //new LZOvl().Compress("tests/lzovl/dec/overlay_0001.dat", "tests/lzovl/cmp/overlay_0001b.bin");
            //new LZOvl().Decompress("tests/lzovl/cmp/overlay_0001b.bin", "tests/lzovl/cmpdec/overlay_0001.dat");

            //Huffman.CompressBlockSize = Huffman.BlockSize.FOURBIT;
            //new Huffman().Compress("tests/huff/dec/00.ffuh.dat", "tests/huff/cmp/00.huff4");
            //new Huffman().Decompress("tests/huff/cmp/00.huff4", "tests/huff/cmpdec/00.dat");
            //new Huffman().Compress("tests/huff/test.dat", "tests/huff/cmp/test.huff");
            //new Huffman().Decompress("tests/huff/cmp/test.huff", "tests/huff/cmpdec/test.dat");

            //new LZOvl().Decompress("tests/lzovl2/overlay_0001.bin", "tests/lzovl2/overlay_0001.dat");

            //new LuminousArc().Decompress("tests/Le/advimg00.imb", "tests/Le/dec/advimg00.imb");

            Console.WriteLine("Success?");
            Console.ReadLine();

        }

    }
}
