using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DSDecmp.Exceptions;
using DSDecmp.Formats;
using DSDecmp.Formats.Nitro;
using DSDecmp.Utils;

namespace DSDecmpEx
{
    public static class Program
    {
#if DEBUG
        public static string PluginFolder = "./Plugins/Debug";
#else
        public static string PluginFolder = "./Plugins";
#endif

        public static void Main(string[] args)
        {
            // I/O input:
            // file                             -> read from file, save to file
            // folder [-co]                     -> read all files from folder, save to folder_dec or folder_cmp (when decomp or comp resp.) (Same filenames)
            // file newfile                     -> read from file, save to newfile
            // folderin folderout [-co]         -> read all files from folderin, save to folderout. (Same filenames)
            // file1 file2 ...                  -> read file1, file2, etc
            // file1 file2 ... folderout [-co]  -> read file1, file2, etc, save to folderout. (same filenames)
            //                                  -> when -co is present, all files that could not be (de)compressed will be copied instead.

            // preambles:
            // <nothing>                        -> decompress input to output using first matched format
            // -d [-ge]                         -> decompress input to output using first matched format. If -ge, then guess the extension based on first 4 bytes.
            // -d [-ge] -f <format>             -> decompress input to output using the indicated format. If -ge, then guess the extension based on first 4 bytes.
            // -c <format> [opt1 opt2 ...]      -> compress input to output using the specified format and its options.

            // built-in formats:
            // lz10         -> LZ-0x10, found in >= GBA
            // lz11         -> LZ-0x11, found in >= NDS
            // lzovl        -> LZ-Ovl/Overlay / backwards LZ, found mostly in NDS overlay files.
            // huff4        -> 4-bit Huffman, found in >= GBA
            // huff8        -> 8-bit Huffman, found in >= GBA
            // huff         -> any Huffman format.
            // gba*         -> any format natively supported by the GBA
            // nds*         -> any format natively supported by the NDS, but not LZ-Ovl
            // when compressing, the best format of the selected set is used. when decompression,
            // only the formats in the selected set are used.

            if (args.Length == 0)
            {
                PrintUsage();
                //Console.ReadLine();
                return;
            }

            if (args[0] == "-c")
            {
                if (args.Length <= 2)
                {
                    Console.WriteLine("Too few arguments.");
                    return;
                }

                CompressionFormat format = GetFormat(args[1]).FirstOrDefault();
                if (format == null)
                {
                    return;
                }

                string[] ioArgs = new string[args.Length - 2];
                Array.Copy(args, 2, ioArgs, 0, ioArgs.Length);

                int optionCount = format.ParseCompressionOptions(ioArgs);
                string[] realIoArgs = new string[ioArgs.Length - optionCount];
                Array.Copy(ioArgs, optionCount, realIoArgs, 0, realIoArgs.Length);

                Compress(realIoArgs, format);
            }
            else if (args[0] == "-d")
            {
                if (args.Length <= 1)
                {
                    PrintUsage();
                    return;
                }

                int ioIdx = 1;
                bool guessExtension = false;
                if (args[ioIdx] == "-ge")
                {
                    guessExtension = true;
                    ioIdx++;
                }

                IEnumerable<CompressionFormat>
                    formats = GetAllFormats(false); // we do not need the built-in composite formats to decompress.
                if (args[ioIdx] == "-f")
                {
                    if (args.Length <= ioIdx + 2)
                    {
                        Console.WriteLine("Too few arguments.");
                        return;
                    }

                    formats = GetFormat(args[ioIdx + 1]);
                    ioIdx += 2;
                }

                if (formats == null)
                {
                    return;
                }

                if (args.Length <= ioIdx)
                {
                    Console.WriteLine("Too few arguments.");
                    return;
                }

                string[] ioArgs = new string[args.Length - ioIdx];
                Array.Copy(args, ioIdx, ioArgs, 0, ioArgs.Length);

                Decompress(ioArgs, formats, guessExtension);
            }
            else
            {
                Decompress(args, GetAllFormats(false), false);
            }
        }

        #region Usage printer

        private static void PrintUsage()
        {
            Console.WriteLine("DSDecmp - Decompressor for compression formats used on the NDS - by Barubary");
            Console.WriteLine();
            Console.WriteLine("Usage:\tDSDecmp FMTARGS IOARGS");
            Console.WriteLine();
            Console.WriteLine("IOARGS can be:");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine("file                     -> read the file, overwrite it.");
            Console.WriteLine("folder [-co]             -> read all files from folder, save to folder_dec");
            Console.WriteLine("                             or folder_cmp.");
            Console.WriteLine("file newfile             -> read the file, save it to newfile.");
            Console.WriteLine("                             (newfile cannot exist yet)");
            Console.WriteLine("folderin folderout [-co] -> read all files from folderin, save to folderout.");
            Console.WriteLine("file1 file2 ...          -> read file1, file2, etc; overwrite them.");
            Console.WriteLine("file1 file2 ... folderout [-co]  -> read file1, file2, etc; save to folderout.");
            Console.WriteLine();
            Console.WriteLine("When -co is present, all files that could not be handled will be copied to the");
            Console.WriteLine("  indicated output folder.");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine();
            Console.WriteLine("FMTARGS can be:");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine("<nothing>              -> try to decompress input to output.");
            Console.WriteLine("-d [-ge]               -> try to decompress input to output.");
            Console.WriteLine("-d [-ge] -f <format>   -> try to decompress input to output, using fiven format");
            Console.WriteLine("-c <format> [opt1 ...] -> compress intput to output using given format ");
            Console.WriteLine("                           and options.");
            Console.WriteLine();
            Console.WriteLine("When -ge is present, the extension of the output file will be determined by");
            Console.WriteLine("  the first 4 bytes of the decompressed data. (of those are alphanuemric ASCII");
            Console.WriteLine("  characters).");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine("Supported formats:");
            Console.WriteLine("<format> -> description");
            foreach (CompressionFormat fmt in GetAllFormats(true))
            {
                Console.WriteLine($"{fmt.CompressionFlag.PadRight(7, ' ')}-> {fmt.Description}");
            }

            Console.WriteLine("-------------------------------------------------------------------------------");
        }

        #endregion

        #region Method: Decompress(string[] ioArgs, IEnumerable<CompressionFormat> formats)

        private static void Decompress(string[] ioArgs, IEnumerable<CompressionFormat> formats, bool guessExtension)
        {
            string[] inputFiles;
            string outputDir;
            bool copyErrors;
            if (!ParseIOArguments(ioArgs, false, out inputFiles, out outputDir, out copyErrors))
                return;

            foreach (string input in inputFiles)
            {
                string outputFile = outputDir ?? IOUtils.GetParent(input);
                if (Directory.Exists(outputDir))
                    outputFile = Path.Combine(outputFile + Path.DirectorySeparatorChar, Path.GetFileName(input));

                try
                {
                    // read the file only once.
                    byte[] inputData;
                    using (Stream inStream = File.OpenRead(input))
                    {
                        inputData = new byte[inStream.Length];
                        inStream.Read(inputData, 0, inputData.Length);
                    }

                    bool decompressed = false;
                    foreach (CompressionFormat format in formats)
                    {
                        if (!format.SupportsDecompression)
                            continue;

                        #region try to decompress using the current format

                        using MemoryStream inStr = new MemoryStream(inputData),
                            outStr = new MemoryStream();
                        if (!format.Supports(inStr, inputData.Length))
                            continue;
                        try
                        {
                            long decompSize = format.Decompress(inStr, inputData.Length, outStr);
                            if (decompSize < 0)
                                continue;
                            if (guessExtension)
                            {
                                string outFileName = Path.GetFileNameWithoutExtension(outputFile);
                                outStr.Position = 0;
                                byte[] magic = new byte[4];
                                outStr.Read(magic, 0, 4);
                                outStr.Position = 0;
                                outFileName += $".{GuessExtension(magic, Path.GetExtension(outputFile).Substring(1))}";
                                outputFile = outputFile.Replace(Path.GetFileName(outputFile), outFileName);
                            }

                            using (FileStream output = File.Create(outputFile))
                            {
                                outStr.WriteTo(output);
                            }

                            decompressed = true;
                            Console.WriteLine($"{format.ShortFormatString}-decompressed {input} to {outputFile}");
                            break;
                        }
                        catch (TooMuchInputException tmie)
                        {
                            // a TMIE is fine. let the user know and continue saving the decompressed data.
                            Console.WriteLine(tmie.Message);
                            if (guessExtension)
                            {
                                string outFileName = Path.GetFileNameWithoutExtension(outputFile);
                                outStr.Position = 0;
                                byte[] magic = new byte[4];
                                outStr.Read(magic, 0, 4);
                                outStr.Position = 0;
                                outFileName += $".{GuessExtension(magic, Path.GetExtension(outputFile).Substring(1))}";
                                outputFile = outputFile.Replace(Path.GetFileName(outputFile), outFileName);
                            }

                            using (FileStream output = File.Create(outputFile))
                            {
                                outStr.WriteTo(output);
                            }

                            decompressed = true;
                            Console.WriteLine($"{format.ShortFormatString}-decompressed {input} to {outputFile}");
                            break;
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        #endregion
                    }

                    if (!decompressed)
                    {
                        #region copy or print and continue

                        if (copyErrors)
                        {
                            Copy(input, outputFile);
                        }
                        else
                            Console.WriteLine($"No suitable decompressor found for {input}.");

                        #endregion
                    }
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"The file {input} does not exist.");
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not load file {input};");
                    Console.WriteLine(ex.Message);
#if DEBUG
                    Console.WriteLine(ex.StackTrace);
#endif
                }
            } // end foreach input
        }

        #endregion Method: Decompress

        #region Method: Compress

        /// <summary>
        /// (Attempts to) Compress the given input to the given output, using the given format.
        /// </summary>
        /// <param name="ioArgs">The I/O arguments from the program input.</param>
        /// <param name="format">The desired format to compress with.</param>
        private static void Compress(string[] ioArgs, CompressionFormat format)
        {
            if (!format.SupportsCompression)
            {
                Console.WriteLine($"Cannot compress using {format.ShortFormatString}; compression is not supported.");
                return;
            }

            string[] inputFiles;
            string outputDir;
            bool copyErrors;
            if (!ParseIOArguments(ioArgs, true, out inputFiles, out outputDir, out copyErrors))
                return;

            foreach (string input in inputFiles)
            {
                string outputFile = outputDir ?? IOUtils.GetParent(input);
                if (Directory.Exists(outputDir))
                    outputFile = Path.Combine(outputFile + Path.DirectorySeparatorChar, Path.GetFileName(input));

                try
                {
                    // read the file only once.
                    byte[] inputData;
                    using (Stream inStream = File.OpenRead(input))
                    {
                        inputData = new byte[inStream.Length];
                        inStream.Read(inputData, 0, inputData.Length);
                    }

                    #region try to compress

                    using (MemoryStream inStr = new MemoryStream(inputData),
                        outStr = new MemoryStream())
                    {
                        try
                        {
                            long compSize = format.Compress(inStr, inputData.Length, outStr);
                            if (compSize > 0)
                            {
                                using (FileStream output = File.Create(outputFile))
                                {
                                    outStr.WriteTo(output);
                                }

                                if (format is CompositeFormat)
                                    Console.Write((format as CompositeFormat).LastUsedCompressFormatString);
                                else
                                    Console.Write(format.ShortFormatString);
                                Console.WriteLine($"-compressed {input} to {outputFile}");
                            }
                        }
                        catch (Exception ex)
                        {
                            #region copy or print and continue

                            if (copyErrors)
                            {
                                Copy(input, outputFile);
                            }
                            else
                            {
                                Console.WriteLine($"Could not {format.ShortFormatString}-compress {input};");
                                Console.WriteLine(ex.Message);
#if DEBUG
                                Console.WriteLine(ex.StackTrace);
#endif
                            }

                            #endregion
                        }
                    }

                    #endregion
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"The file {input} does not exist.");
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not load file {input};");
                    Console.WriteLine(ex.Message);
#if DEBUG
                    Console.WriteLine(ex.StackTrace);
#endif
                }
            } // end foreach input
        }

        #endregion Method: Compress

        #region Method: ParseIOArguments

        /// <summary>
        /// Parses the IO arguments of the input.
        /// </summary>
        /// <param name="ioArgs">The arguments to parse.</param>
        /// <param name="compress">If the arguments are used for compression. If not, decompression is assumed. (used for default output folder name)</param>
        /// <param name="inputFiles">The files to handle as input.</param>
        /// <param name="outputDir">The directory to save the handled files in. If this is null,
        /// the files should be overwritten. If this does not exist, it is the output file
        /// (the input may only contain one file if that si the case).</param>
        /// <param name="copyErrors">If files that cannot be handled (properly) should be copied to the output directory.</param>
        /// <returns>True iff parsing of the arguments succeeded.</returns>
        private static bool ParseIOArguments(string[] ioArgs, bool compress, out string[] inputFiles,
            out string outputDir, out bool copyErrors)
        {
            inputFiles = null;
            // when null, output dir = input dir. if it does not exist, it is the output file (only possible when only one input file).
            outputDir = null;
            copyErrors = false;

            #region check if the -co flag is present

            if (ioArgs.Length > 0 && ioArgs[ioArgs.Length - 1] == "-co")
            {
                string[] newIoArgs = new string[ioArgs.Length - 1];
                Array.Copy(ioArgs, newIoArgs, newIoArgs.Length);
                ioArgs = newIoArgs;
                copyErrors = true;
            }

            #endregion

            switch (ioArgs.Length)
            {
                case 0:
                    Console.WriteLine("No input file given.");
                    return false;
                case 1:
                    if (Directory.Exists(ioArgs[0]))
                    {
                        inputFiles = Directory.GetFiles(ioArgs[0]);
                        if (compress)
                            outputDir = $"{Path.GetFullPath(ioArgs[0])}_cmp";
                        else
                            outputDir = $"{Path.GetFullPath(ioArgs[0])}_dec";
                        if (!Directory.Exists(outputDir))
                            Directory.CreateDirectory(outputDir);
                        break;
                    }
                    else if (File.Exists(ioArgs[0]))
                    {
                        inputFiles = ioArgs;
                        outputDir = null;
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"The file {ioArgs[0]} does not exist.");
                        return false;
                    }
                case 2:
                    if (Directory.Exists(ioArgs[0]))
                    {
                        inputFiles = Directory.GetFiles(ioArgs[0]);
                        outputDir = ioArgs[1];
                        if (!Directory.Exists(outputDir))
                            Directory.CreateDirectory(outputDir);
                        break;
                    }
                    else if (File.Exists(ioArgs[0]))
                    {
                        if (File.Exists(ioArgs[1]))
                        {
                            inputFiles = ioArgs;
                            outputDir = null;
                            break;
                        }
                        else // if (Directory.Exists(ioArgs[1]))
                            // both nonexisting file and existing directory is handled the same.
                        {
                            inputFiles = new[] {ioArgs[0]};
                            outputDir = ioArgs[1];
                            break;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"The file {ioArgs[0]} does not exist.");
                        return false;
                    }
                default:
                    if (File.Exists(ioArgs[ioArgs.Length - 1]))
                    {
                        inputFiles = ioArgs;
                        outputDir = null;
                        break;
                    }
                    else //if (Directory.Exists(ioArgs[ioArgs.Length - 1]))
                        // both existing and nonexisting directories are fine.
                    {
                        outputDir = ioArgs[ioArgs.Length - 1];
                        inputFiles = new string[ioArgs.Length - 1];
                        Array.Copy(ioArgs, inputFiles, inputFiles.Length);

                        // but we must make sure the output directory exists.
                        if (!Directory.Exists(outputDir))
                            Directory.CreateDirectory(outputDir);
                        break;
                    }
            }

            return true;
        }

        #endregion ParseIOArguments

        #region Method: GuessExtension(magic, defaultExt)

        /// <summary>
        /// Guess the extension of a file by looking at the given magic bytes of a file.
        /// If they are alphanumeric (without accents), they could indicate the type of file.
        /// If no sensible extension could be found from the magic bytes, the given default extension is returned.
        /// </summary>
        private static string GuessExtension(byte[] magic, string defaultExt)
        {
            string ext = "";
            for (int i = 0; i < magic.Length && i < 4; i++)
            {
                if ((magic[i] >= 'a' && magic[i] <= 'z') || (magic[i] >= 'A' && magic[i] <= 'Z')
                                                         || char.IsDigit((char)magic[i]))
                {
                    ext += (char)magic[i];
                }
                else
                    break;
            }

            if (ext.Length <= 1)
                return defaultExt;
            return ext;
        }

        #endregion

        /// <summary>
        /// Copies the source file to the destination path.
        /// </summary>
        private static void Copy(string sourcefile, string destfile)
        {
            if (Path.GetFullPath(sourcefile) == Path.GetFullPath(destfile))
                return;
            File.Copy(sourcefile, destfile);
            Console.WriteLine($"Copied {sourcefile} to {destfile}");
        }

        #region Format sequence getters

        /// <summary>
        /// Gets the compression format corresponding to the given format string.
        /// </summary>
        private static IEnumerable<CompressionFormat> GetFormat(string formatstring)
        {
            if (formatstring == null)
                yield break;
            foreach (CompressionFormat fmt in GetAllFormats(true))
                if (fmt.CompressionFlag == formatstring)
                {
                    yield return fmt;
                    yield break;
                }

            Console.WriteLine($"No such compression format: {formatstring}");
        }

        /// <summary>
        /// Gets a sequence over all compression formats currently supported; both built-in and plugin-based.
        /// </summary>
        private static IEnumerable<CompressionFormat> GetAllFormats(bool alsoBuiltInCompositeFormats)
        {
            foreach (CompressionFormat fmt in GetBuiltInFormats(alsoBuiltInCompositeFormats))
                yield return fmt;
            foreach (CompressionFormat fmt in GetPluginFormats())
                yield return fmt;
        }

        /// <summary>
        /// Gets a sequence over all built-in compression formats.
        /// </summary>
        /// <param name="alsoCompositeFormats">If the built-in composite formats should also be part of the sequence.</param>
        private static IEnumerable<CompressionFormat> GetBuiltInFormats(bool alsoCompositeFormats)
        {
            yield return new LZOvl();
            yield return new LZ10();
            yield return new LZ11();
            yield return new Huffman4();
            yield return new Huffman8();
            yield return new RLE();
            yield return new NullCompression();
            if (alsoCompositeFormats)
            {
                yield return new HuffmanAny();
                yield return new CompositeGBAFormat();
                yield return new CompositeNDSFormat();
            }
        }

        /// <summary>
        /// Gets a sequence over all formats that can be used from plugins.
        /// </summary>
        private static IEnumerable<CompressionFormat> GetPluginFormats()
        {
            string pluginPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
            pluginPath = Path.Combine(pluginPath, PluginFolder);
            if (Directory.Exists(pluginPath))
            {
                foreach (CompressionFormat fmt in IOUtils.LoadCompressionPlugins(pluginPath))
                    yield return fmt;
            }
            else
            {
                Console.WriteLine($"Plugin folder {pluginPath} is not present; only built-in formats are supported.");
            }
        }

        #endregion
    }
}
