using System;
using System.Collections.Generic;
using System.Text;

namespace DSDecmp.Formats.Nitro
{
    /// <summary>
    /// Base class for Nitro-based decompressors. Uses the 1-byte magic and 3-byte decompression
    /// size format.
    /// </summary>
    public abstract class NitroCFormat : CompressionFormat
    {
        /// <summary>
        /// If true, Nitro Decompressors will not decompress files that have a decompressed
        /// size (plaintext size) larger than MaxPlaintextSize.
        /// </summary>
        public static bool SkipLargePlaintexts = true;
        /// <summary>
        /// The maximum allowed size of the decompressed file (plaintext size) allowed for Nitro
        /// Decompressors. Only used when SkipLargePlaintexts = true.
        /// </summary>
        public static int MaxPlaintextSize = 0x180000;

        /// <summary>
        /// The first byte of every file compressed with the format for this particular
        /// Nitro Dcompressor instance.
        /// </summary>
        protected byte magicByte;

        public NitroCFormat(byte magicByte)
        {
            this.magicByte = magicByte;
        }

        /// <summary>
        /// Converts an array of (at least) 3 bytes into an integer, using the format used
        /// in Nitro compression formats to store the decompressed size.
        /// If the size is not 3, the fourth byte will also be included.
        /// </summary>
        protected int Bytes2Size(byte[] bytes)
        {
            if (bytes.Length == 3)
                return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
            else
                return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24);
        }

        public override bool Supports(System.IO.Stream stream)
        {
            long startPosition = stream.Position;
            try
            {
                int firstByte = stream.ReadByte();
                if (firstByte != this.magicByte)
                    return false;
                // no need to read the size info as well if it's used anyway.
                if (!SkipLargePlaintexts)
                    return true;
                byte[] sizeBytes = new byte[3];
                stream.Read(sizeBytes, 0, 3);
                int outSize = this.Bytes2Size(sizeBytes);
                if (outSize == 0)
                {
                    sizeBytes = new byte[4];
                    stream.Read(sizeBytes, 0, 4);
                    outSize = this.Bytes2Size(sizeBytes);
                }
                return outSize <= MaxPlaintextSize;
            }
            finally
            {
                stream.Position = startPosition;
            }
        }
    }
}
