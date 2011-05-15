using System;
using System.Collections.Generic;
using System.Text;
using DSDecmp.Formats.Nitro;
using DSDecmp.Formats;
using System.IO;

namespace DSDecmp
{
    public static class NewProgram
    {
        /// <summary>
        /// The formats allowed when compressing a file.
        /// </summary>
        public enum Formats
        {
            LZOVL, // keep this as the first one, as only the end of a file may be LZ-ovl-compressed (and overlay files are oftenly double-compressed)
            LZ10,
            LZ11,
            HUFF4,
            HUFF8,
            RLE,
            HUFF,
            NDS,
            GBA,
        }

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
#if DEBUG
                Console.ReadLine();
#endif
                return;
            }

            int argIndex = 0;
            bool compress = false;
            Formats compressFormat = Formats.NDS;

            #region check for the -c option and its parameter(s)
            if (args[argIndex].Equals("-c"))
            {
                argIndex++;
                compress = true;

                if (args.Length < argIndex + 2)
                {
                    Console.WriteLine("A compression format and input file is required in order to compress.");
                    Console.WriteLine();
                    PrintUsage();
                    return;
                }
                switch (args[argIndex].ToLower())
                {
                    case "lz10": compressFormat = Formats.LZ10; break;
                    case "lz11": compressFormat = Formats.LZ11; break;
                    case "lzovl": compressFormat = Formats.LZOVL; break;
                    case "rle": compressFormat = Formats.RLE; break;
                    case "huff4": compressFormat = Formats.HUFF4; break;
                    case "huff8": compressFormat = Formats.HUFF8; break;
                    case "huff": compressFormat = Formats.HUFF; break;
                    case "gba*": compressFormat = Formats.GBA; break;
                    case "nds*": compressFormat = Formats.NDS; break;
                    default:
                        Console.WriteLine("Unknown compression format " + args[argIndex]);
                        Console.WriteLine();
                        PrintUsage();
                        return;
                }
                argIndex++;
                // handle the format options
                switch (compressFormat)
                {
                    case Formats.LZ10:
                    case Formats.GBA:
                        if (args[argIndex].Equals("-opt"))
                        {
                            LZ10.LookAhead = true;
                            argIndex++;
                        }
                        break;
                    case Formats.LZ11:
                        if (args[argIndex].Equals("-opt"))
                        {
                            LZ11.LookAhead = true;
                            argIndex++;
                        }
                        break;
                    case Formats.LZOVL:
                        if (args[argIndex].Equals("-opt"))
                        {
                            LZOvl.LookAhead = true;
                            argIndex++;
                        }
                        break;
                    case Formats.NDS:
                        if (args[argIndex].Equals("-opt"))
                        {
                            LZ10.LookAhead = true;
                            LZ11.LookAhead = true;
                            LZOvl.LookAhead = true;
                            argIndex++;
                        }
                        break;
                }
            }
            #endregion

            if (args.Length < argIndex + 1)
                throw new ArgumentException("No input file given.");

            bool guessExtension = false;
            if (args[argIndex].Equals("-ge"))
            {
                guessExtension = true;
                argIndex++;
            }

            if (args.Length < argIndex + 1)
                throw new ArgumentException("No input file given.");

            string input = args[argIndex++];
            string output = null;
            if (args.Length > argIndex)
                output = args[argIndex++];

            if (compress)
                Compress(input, output, compressFormat, guessExtension);
            else
                Decompress(input, output, guessExtension);

#if DEBUG
            Console.ReadLine();
#endif
        }

        private static void PrintUsage()
        {
            Console.WriteLine("DSDecmp - Decompressor for compression formats used on the NDS - by Barubary");
            Console.WriteLine();
            Console.WriteLine("Usage:\tDSDecmp (-c FORMAT FORMATOPT*) (-ge) input (output)");
            Console.WriteLine();
            Console.WriteLine("Without the -c modifier, DSDecmp will decompress the input file to the output");
            Console.WriteLine(" file. If the output file is a directory, the output file will be placed in");
            Console.WriteLine(" that directory with the same filename as the original file. The extension will");
            Console.WriteLine(" be appended with a format-specific extension.");
            Console.WriteLine("The input can also be a directory. In that case, it would be the same as");
            Console.WriteLine(" calling DSDecmp for every non-directory in the given directory with the same");
            Console.WriteLine(" options, with one exception; the output is by default the input folder, but");
            Console.WriteLine(" with '_dec' appended.");
            Console.WriteLine("If the output does not exist, it is assumed to be the same type as the input");
            Console.WriteLine(" (file or directory).");
            Console.WriteLine("If there is no output file given, it is assumed to be the directory of the");
            Console.WriteLine(" input file.");
            Console.WriteLine();
            Console.WriteLine("With the -ge option, instead of a format-specific extension, the extension");
            Console.WriteLine(" will be guessed from the first four bytes of the output file. Only");
            Console.WriteLine(" non-accented letters or numbers are considered in those four bytes.");
            Console.WriteLine();
            Console.WriteLine("With the -c option, the input is compressed instead of decompressed. FORMAT");
            Console.WriteLine("indicates the desired compression format, and can be one of:");
            Console.WriteLine(" --- formats built-in in the NDS ---");
            Console.WriteLine("    lz10  - 'default' LZ-compression format.");
            Console.WriteLine("    lz11  - LZ-compression format better suited for files with long repetitions");
            Console.WriteLine("    lzovl - LZ-compression used in 'overlay files'.");
            Console.WriteLine("    rle   - Run-Length Encoding 'compression'.");
            Console.WriteLine("    huff4 - Huffman compression with 4-bit sized data blocks.");
            Console.WriteLine("    huff8 - Huffman compression with 8-bit sized data blocks.");
            Console.WriteLine(" --- utility 'formats' ---");
            Console.WriteLine("    huff  - The Huffman compression that gives the bext compression ratio.");
            Console.WriteLine("    nds*  - The built-in compression format that gives the best compression");
            Console.WriteLine("            ratio. Will never compress using lzovl.");
            Console.WriteLine("    gba*  - The built-in compression format that gives the best compression");
            Console.WriteLine("            ratio, and is also supported by the GBA.");
            Console.WriteLine();
            Console.WriteLine("The following format options (FORMATOPT) are available:");
            Console.WriteLine(" lz10, lz11, lzovl, gba* and nds*:");
            Console.WriteLine("    -opt  : employs a better compression algorithm to boost the compression");
            Console.WriteLine("            ratio. Not using this option will result in using the algorithm");
            Console.WriteLine("            originally used to compress the game files.");
            Console.WriteLine("            Using this option for the gba* and nds* will only have effect on");
            Console.WriteLine("            the lz10, lz11 and lzovl algorithms.");
            Console.WriteLine();
            Console.WriteLine("If the input is a directory when the -c option, the default output directory");
            Console.WriteLine(" is the input directory appended with '_cmp'.");
            Console.WriteLine();
            Console.WriteLine("Supplying the -ge modifier together with the -c modifier, the extension of the");
            Console.WriteLine(" compressed files will be extended with the 'FORMAT' value that always results");
            Console.WriteLine(" in that particualr format (so 'lz11', 'rle', etc).");
            Console.WriteLine("If the -ge modifier is not present, the extension of compressed files will be");
            Console.WriteLine(" extended with .cdat");

        }

        #region compression methods

        private static void Compress(string input, string output, Formats format, bool guessExtension)
        {
            if (!File.Exists(input) && !Directory.Exists(input))
            {
                Console.WriteLine("Cannot compress a file or directory that does not exist (" + input + ")");
                return;
            }

            // set the default value of the output
            if (string.IsNullOrEmpty(output))
            {
                if (Directory.Exists(input))
                {
                    string newDir = Path.GetFullPath(input) + "_cmp";
                    if (!Directory.Exists(newDir))
                        Directory.CreateDirectory(newDir);
                    foreach (string file in Directory.GetFiles(input))
                    {
                        Compress(file, newDir, format, guessExtension);
                    }
                    return;
                }
                else
                {
                    if (!guessExtension)
                        output = input; // the .cdat extension is added automatically
                    else
                        output = Path.GetDirectoryName(input);
                }
            }

            if (Directory.Exists(input))
            {
                if (!Directory.Exists(output))
                    Directory.CreateDirectory(output);
                foreach (string file in Directory.GetFiles(input))
                {
                    Compress(file, output, format, guessExtension);
                }
                return;
            }


            // compress the input
            MemoryStream compressedData = new MemoryStream();
            Formats compressedFormat;
            int outsize = DoCompress(input, compressedData, format, out compressedFormat);
            if (outsize < 0)
                return;

            bool mustAppendExt = !Directory.Exists(output) && !File.Exists(output);
            if (Directory.Exists(output))
            {
                output = CombinePaths(output, Path.GetFileName(input));
            }
            if (mustAppendExt && Path.GetExtension(output) == ".dat")
                output = RemoveExtension(output);
            if (guessExtension)
                output += "." + compressedFormat.ToString().ToLower();
            else if (mustAppendExt)
                output += ".cdat";

            using (FileStream outStream = File.Create(output))
            {
                compressedData.WriteTo(outStream);
                Console.WriteLine(compressedFormat.ToString() + "-compressed " + input + " to " + output);
            }
        }

        private static int DoCompress(string infile, MemoryStream output, Formats format, out Formats actualFormat)
        {
            CompressionFormat fmt = null;
            switch (format)
            {
                case Formats.LZ10: fmt = new LZ10(); break;
                case Formats.LZ11: fmt = new LZ11(); break;
                case Formats.LZOVL: fmt = new LZOvl(); break;
                case Formats.RLE: fmt = new RLE(); break;
                case Formats.HUFF4: Huffman.CompressBlockSize = Huffman.BlockSize.FOURBIT; fmt = new Huffman(); break;
                case Formats.HUFF8: Huffman.CompressBlockSize = Huffman.BlockSize.EIGHTBIT; fmt = new Huffman(); break;
                case Formats.HUFF:
                    return CompressHuff(infile, output, out actualFormat);
                case Formats.GBA:
                    return CompressGBA(infile, output, out actualFormat);
                case Formats.NDS:
                    return CompressNDS(infile, output, out actualFormat);
                default:
                    throw new Exception("Unhandled compression format " + format);
            }
            actualFormat = format;

            using (FileStream inStream = File.OpenRead(infile))
            {
                try
                {
                    return fmt.Compress(inStream, inStream.Length, output);
                }
                catch (Exception s)
                {
                    // any exception generated by compression is a fatal exception
                    Console.WriteLine(s.Message);
                    return -1;
                }
            }
        }

        private static int CompressHuff(string infile, MemoryStream output, out Formats actualFormat)
        {
            return CompressBest(infile, output, out actualFormat, Formats.HUFF4, Formats.HUFF8);
        }

        private static int CompressGBA(string infile, MemoryStream output, out Formats actualFormat)
        {
            return CompressBest(infile, output, out actualFormat, Formats.HUFF4, Formats.HUFF8, Formats.LZ10, Formats.RLE);
        }

        private static int CompressNDS(string infile, MemoryStream output, out Formats actualFormat)
        {
            return CompressBest(infile, output, out actualFormat, Formats.HUFF4, Formats.HUFF8, Formats.LZ10, Formats.LZ11, Formats.RLE);
        }

        private static int CompressBest(string infile, MemoryStream output, out Formats actualFormat, params Formats[] formats)
        {
            // only read the input data once from the file.
            byte[] inputData;
            using (FileStream inStream = File.OpenRead(infile))
            {
                inputData = new byte[inStream.Length];
                inStream.Read(inputData, 0, inputData.Length);
            }

            MemoryStream bestOutput = null;
            int minCompSize = int.MaxValue;
            actualFormat = Formats.GBA;
            foreach (Formats format in formats)
            {
                #region compress the file in each format, and save the best one

                MemoryStream currentOutput = new MemoryStream();
                CompressionFormat realFormat = null;
                switch (format)
                {
                    case Formats.HUFF4: Huffman.CompressBlockSize = Huffman.BlockSize.FOURBIT; realFormat = new Huffman(); break;
                    case Formats.HUFF8: Huffman.CompressBlockSize = Huffman.BlockSize.EIGHTBIT; realFormat = new Huffman(); break;
                    case Formats.LZ10: realFormat = new LZ10(); break;
                    case Formats.LZ11: realFormat = new LZ11(); break;
                    case Formats.LZOVL: realFormat = new LZOvl(); break;
                    case Formats.RLE: realFormat = new RLE(); break;
                }

                int currentOutSize;
                try
                {
                    using (MemoryStream inStream = new MemoryStream(inputData))
                    {
                        currentOutSize = realFormat.Compress(inStream, inStream.Length, currentOutput);
                    }
                }
                catch (InputTooLargeException i)
                {
                    Console.WriteLine(i.Message);
                    actualFormat = format;
                    return -1;
                }
                catch (Exception)
                {
                    continue;
                }
                if (currentOutSize < minCompSize)
                {
                    bestOutput = currentOutput;
                    minCompSize = currentOutSize;
                    actualFormat = format;
                }

                #endregion
            }

            if (bestOutput == null)
            {
                Console.WriteLine("The file could not be compressed in any format.");
                return -1;
            }
            bestOutput.WriteTo(output);
            return minCompSize;
        }

        #endregion

        #region decompression methods

        private static void Decompress(string input, string output, bool guessExtension)
        {
            if (!File.Exists(input) && !Directory.Exists(input))
            {
                Console.WriteLine("Cannot decompress a file or directory that does not exist (" + input + ")");
                return;
            }

            // set the default value of the output
            if (string.IsNullOrEmpty(output))
            {
                if (Directory.Exists(input))
                {
                    string newDir = Path.GetFullPath(input) + "_dec";
                    if (!Directory.Exists(newDir))
                        Directory.CreateDirectory(newDir);
                    foreach (string file in Directory.GetFiles(input))
                    {
                        Decompress(file, newDir, guessExtension);
                    }
                    return;
                }
                else
                {
                    if (!guessExtension)
                        output = input; // '.dat' gets added automatically if -ge is not given
                    else
                        output = Path.GetDirectoryName(input);
                }
            }

            if (Directory.Exists(input))
            {
                if (File.Exists(output))
                {
                    Console.WriteLine("Cannot decompress a folder to a single file.");
                    return;
                }
                if (!Directory.Exists(output))
                    Directory.CreateDirectory(output);
                foreach (string file in Directory.GetFiles(input))
                {
                    Decompress(file, output, guessExtension);
                }
                return;
            }

            byte[] inData;
            using (FileStream inStream = File.OpenRead(input))
            {
                inData = new byte[inStream.Length];
                inStream.Read(inData, 0, inData.Length);
            }

            MemoryStream decompressedData = new MemoryStream();
            long decSize = -1;
            Formats usedFormat = Formats.NDS;
            // just try all formats, and stop once one has been found that can decompress it.
            foreach (Formats f in Enum.GetValues(typeof(Formats)))
            {
                using (MemoryStream inStream = new MemoryStream(inData))
                {
                    decSize = Decompress(inStream, decompressedData, f);
                    if (decSize >= 0)
                    {
                        usedFormat = f;
                        break;
                    }
                }
            }
            if (decSize < 0)
            {
                Console.WriteLine("Could not decompress " + input + "; no matching compression method found.");
                return;
            }

            bool mustAppendExt = !Directory.Exists(output) && !File.Exists(output);

            if (Directory.Exists(output))
            {
                output = CombinePaths(output, Path.GetFileName(input));
            }

            byte[] outData = decompressedData.ToArray();
            if (mustAppendExt)
            {
                switch (Path.GetExtension(output))
                {
                    case ".cdat":
                    case ".lz10":
                    case ".lz11":
                    case ".lzovl":
                    case ".rle":
                    case ".huff4":
                    case ".huff8":
                        output = RemoveExtension(output);
                        break;
                }
            }
            if (guessExtension)
            {
                string ext = "";
                for (int i = 0; i < 4; i++)
                {
                    if ((outData[i] >= 'a' && outData[i] <= 'z')
                        || (outData[i] >= 'A' && outData[i] <= 'Z')
                        || char.IsDigit((char)outData[i]))
                        ext += (char)outData[i];
                    else
                        break;
                }
                if (ext.Length > 0)
                    output += "." + ext;
                else
                    output += ".dat";
            }
            else if(mustAppendExt)
                output += ".dat";

            using (FileStream outStream = File.Create(output))
            {
                outStream.Write(outData, 0, outData.Length);
                Console.WriteLine(usedFormat.ToString() + "-decompressed " + input + " to " + output);
            }

        }

        private static long Decompress(MemoryStream inputStream, MemoryStream output, Formats format)
        {
            CompressionFormat realFormat = null;
            switch (format)
            {
                case Formats.HUFF:
                    realFormat = new Huffman(); break;
                case Formats.LZ10:
                    realFormat = new LZ10(); break;
                case Formats.LZ11:
                    realFormat = new LZ11(); break;
                case Formats.LZOVL:
                    realFormat = new LZOvl(); break;
                case Formats.RLE:
                    realFormat = new RLE(); break;
                default:
                    return -1;
            }
            if (!realFormat.Supports(inputStream, inputStream.Length))
                return -1;
            try
            {
                return realFormat.Decompress(inputStream, inputStream.Length, output);
            }
            catch (TooMuchInputException e)
            {
                Console.WriteLine(e.Message);
                return output.Length;
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not decompress using the " + format.ToString() + " format; " + e.Message);
                return -1;
            }
        }

        #endregion

        private static string CombinePaths(string dir, string file)
        {
            if (Path.IsPathRooted(file))
                return file;
            if (!dir.EndsWith(Path.DirectorySeparatorChar + "")
                && !dir.EndsWith(Path.AltDirectorySeparatorChar + ""))
                return dir + Path.DirectorySeparatorChar + file;
            else
                return dir + file;
        }
        private static string RemoveExtension(string path)
        {
            return Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path);
        }
    }
}
