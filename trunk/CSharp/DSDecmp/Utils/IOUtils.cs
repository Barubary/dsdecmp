using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;

namespace DSDecmp
{
    /// <summary>
    /// Class for I/O-related utility methods.
    /// </summary>
    public static class IOUtils
    {

        #region byte[] <-> (u)int
        /// <summary>
        /// Returns a 4-byte unsigned integer as used on the NDS converted from four bytes
        /// at a specified position in a byte array.
        /// </summary>
        /// <param name="buffer">The source of the data.</param>
        /// <param name="offset">The location of the data in the source.</param>
        /// <returns>The indicated 4 bytes converted to uint</returns>
        public static uint ToNDSu32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset]
                        | (buffer[offset + 1] << 8)
                        | (buffer[offset + 2] << 16)
                        | (buffer[offset + 3] << 24));
        }

        /// <summary>
        /// Returns a 4-byte signed integer as used on the NDS converted from four bytes
        /// at a specified position in a byte array.
        /// </summary>
        /// <param name="buffer">The source of the data.</param>
        /// <param name="offset">The location of the data in the source.</param>
        /// <returns>The indicated 4 bytes converted to int</returns>
        public static int ToNDSs32(byte[] buffer, int offset)
        {
            return (int)(buffer[offset]
                        | (buffer[offset + 1] << 8)
                        | (buffer[offset + 2] << 16)
                        | (buffer[offset + 3] << 24));
        }

        /// <summary>
        /// Converts a u32 value into a sequence of bytes that would make ToNDSu32 return
        /// the given input value.
        /// </summary>
        public static byte[] FromNDSu32(uint value)
        {
            return new byte[] {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF)
            };
        }

        /// <summary>
        /// Returns a 3-byte integer as used in the built-in compression
        /// formats in the DS, convrted from three bytes at a specified position in a byte array,
        /// </summary>
        /// <param name="buffer">The source of the data.</param>
        /// <param name="offset">The location of the data in the source.</param>
        /// <returns>The indicated 3 bytes converted to an integer.</returns>
        public static int ToNDSu24(byte[] buffer, int offset)
        {
            return (int)(buffer[offset]
                        | (buffer[offset + 1] << 8)
                        | (buffer[offset + 2] << 16));
        }
        #endregion

        #region Plugin loading
        /// <summary>
        /// (Attempts to) load compression formats from the given file.
        /// </summary>
        /// <param name="file">The dll file to load.</param>
        /// <param name="printFailures">If formats without an empty contrsuctor should get a print.</param>
        /// <returns>A list with an instance of all compression formats found in the given dll file.</returns>
        /// <exception cref="FileNotFoundException">If the given file does not exist.</exception>
        /// <exception cref="FileLoadException">If the file could not be loaded.</exception>
        /// <exception cref="BadImageFormatException">If the file is not a valid assembly, or the loaded
        /// assembly is compiled with a higher version of .NET.</exception>
        internal static IEnumerable<CompressionFormat> LoadCompressionPlugin(string file, bool printFailures = false)
        {
            if (file == null)
                throw new FileNotFoundException("A null-path cannot be loaded.");
            List<CompressionFormat> newFormats = new List<CompressionFormat>();

            string fullPath = Path.GetFullPath(file);

            Assembly dll = Assembly.LoadFile(fullPath);
            foreach (Type dllType in dll.GetTypes())
            {
                if (dllType.IsSubclassOf(typeof(CompressionFormat))
                    && !dllType.IsAbstract)
                {
                    try
                    {
                        newFormats.Add(Activator.CreateInstance(dllType) as CompressionFormat);
                    }
                    catch (MissingMethodException)
                    {
                        if (printFailures)
                            Console.WriteLine(dllType + " is a compression format, but does not have a parameterless constructor. Format cannot be loaded from " + fullPath + ".");
                    }
                }
            }

            return newFormats;
        }

        /// <summary>
        /// Loads all compression formats found in the given folder.
        /// </summary>
        /// <param name="folder">The folder to load plugins from.</param>
        /// <returns>A list with an instance of all compression formats found in the given folder.</returns>
        internal static IEnumerable<CompressionFormat> LoadCompressionPlugins(string folder)
        {
            List<CompressionFormat> formats = new List<CompressionFormat>();

            foreach (string file in Directory.GetFiles(folder))
            {
                try
                {
                    formats.AddRange(LoadCompressionPlugin(file, false));
                }
                catch (Exception) { }
            }

            return formats;
        }
        #endregion

        /// <summary>
        /// Gets the full path to the parent directory of the given path.
        /// </summary>
        /// <param name="path">The path to get the parent directory path of.</param>
        /// <returns>The full path to the parent directory of teh given path.</returns>
        public static string GetParent(string path)
        {
            return Directory.GetParent(path).FullName;
        }
    }
}
