using System;
using System.Collections.Generic;
using System.Text;

namespace DSDecmp
{
    public static class NewProgram
    {
        public static void Main3(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
#if DEBUG
                Console.ReadLine();
#endif
                return;
            }
                

#if DEBUG
            Console.ReadLine();
#endif
        }

        private static void PrintUsage()
        {
            Console.WriteLine("DSDecmp - Decompressor for compression formats used on the NDS - by Barubary");
            Console.WriteLine();
            Console.WriteLine("Usage:\tDSDecmp (-c FORMAT (FORMATOPTS)) (-ge) input (output)");
            Console.WriteLine();
            Console.WriteLine("Without the -c modifier, DSDecmp will decompress the input file to the output");
            Console.WriteLine("file. If the output file is a directory, the output file will be placed in that");
            Console.WriteLine("directory with the same filename as the original file. The extension will be");
            Console.WriteLine("appended with a format-specific extension.");
            Console.WriteLine("The input can also be a directory. In that case, it would be the same as");
            Console.WriteLine("calling DSDecmp for every non-directory in the given directory with the same");
            Console.WriteLine("options, with one exception; the output is by default the input folder, but");
            Console.WriteLine("with '_dec' appended.");
            Console.WriteLine("If there is no output file given, it is assumed to be the directory of the");
            Console.WriteLine("input file.");
            Console.WriteLine();
            Console.WriteLine("With the -ge option, instead of a format-specific extension, the extension");
            Console.WriteLine("will be guessed from the first four bytes of the output file. Only non-accented");
            Console.WriteLine("letters or numbers are considered in those four bytes.");
            Console.WriteLine();
            Console.WriteLine("With the -c option, the input is compressed instead of decompressed. FORMAT");
            Console.WriteLine("indicates the desired compression format, and can be one of:");
            Console.WriteLine(" --- formats built-in in the NDS ---");
            Console.WriteLine("    lz10  - 'default' LZ-compression format.");
            Console.WriteLine("    lz11  - LZ-compression format better suited for files with long repetitions.");
            Console.WriteLine("    lzovl - LZ-compression used in 'overlay files'.");
            Console.WriteLine("    rle   - Run-Length Encoding 'compression'.");
            Console.WriteLine("    huff4 - Huffman compression with 4-bit sized data blocks.");
            Console.WriteLine("    huff8 - Huffman compression with 8-bit sized data blocks.");
            Console.WriteLine(" --- utility 'formats' ---");
            Console.WriteLine("    huff  - The Huffman compression that gives the bext compression ratio.");
            Console.WriteLine("    nds*  - The built-in compression format that gives the best compression");
            Console.WriteLine("            ratio.");
            Console.WriteLine("    gba*  - The built-in compression format that gives the best compression");
            Console.WriteLine("            ratio, and is also supported by the GBA.");
            Console.WriteLine();
            Console.WriteLine("The following format options are available:");
            Console.WriteLine(" lz10, lz11 and lzovl:");
            Console.WriteLine("    -opt  : employs a better compression algorithm to boost the compression");
            Console.WriteLine("            ratio. Not using this option will result in using the algorithm");
            Console.WriteLine("            originally used to compress the game files.");
            Console.WriteLine();
            Console.WriteLine("Supplying the -ge modifier together with the -c modifier, the extension of the");
            Console.WriteLine("compressed files will be extended with the 'FORMAT' value that always results");
            Console.WriteLine("in that particualr format (so 'lz11', 'rle', etc).");
            Console.WriteLine("If the -ge modifier is not present, the extension of compressed files will be");
            Console.WriteLine("extended with .cdat");

        }
    }
}
