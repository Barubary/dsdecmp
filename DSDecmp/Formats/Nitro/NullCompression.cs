using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DSDecmp.Formats.Nitro
{
    /// <summary>
    /// 'Compression' format without any compression whatsoever.
    /// Compression using this format will only prepend 0x00 plus the original file size to the file.
    /// </summary>
    public class NullCompression : NitroCFormat
    {
        /// <summary>
        /// Gets a short string identifying this compression format.
        /// </summary>
        public override string ShortFormatString
        {
            get { return "NULL"; }
        }

        /// <summary>
        /// Gets a short description of this compression format (used in the program usage).
        /// </summary>
        public override string Description
        {
            get { return "NULL-'compression' format. Prefixes file with 0x00 and filesize."; }
        }

        /// <summary>
        /// Gets if this format supports compressing a file.
        /// </summary>
        public override bool SupportsCompression
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the value that must be given on the command line in order to compress using this format.
        /// </summary>
        public override string CompressionFlag
        {
            get { return "null"; }
        }

        /// <summary>
        /// Creates a new instance of the NULL-compression format.
        /// </summary>
        public NullCompression()
            : base(0) { }

        /// <summary>
        /// Checks if the given stream is (or could be) 'compressed' using the NULL compression format.
        /// </summary>
        public override bool Supports(System.IO.Stream stream, long inLength)
        {
            long startPosition = stream.Position;
            try
            {
                int firstByte = stream.ReadByte();
                if (firstByte != 0)
                    return false;
                byte[] sizeBytes = new byte[3];
                stream.Read(sizeBytes, 0, 3);
                int outSize = IOUtils.ToNDSu24(sizeBytes, 0);
                int headerSize = 4;
                if (outSize == 0)
                {
                    sizeBytes = new byte[4];
                    stream.Read(sizeBytes, 0, 4);
                    outSize = (int)IOUtils.ToNDSu32(sizeBytes, 0);
                    headerSize = 8;
                }
                return outSize == inLength - headerSize;
            }
            finally
            {
                stream.Position = startPosition;
            }
        }

        /// <summary>
        /// 'Decompresses' the given input stream using the NULL format.
        /// </summary>
        public override long Decompress(System.IO.Stream instream, long inLength, System.IO.Stream outstream)
        {
            long readBytes = 0;

            byte type = (byte)instream.ReadByte();
            if (type != base.magicByte)
                throw new InvalidDataException("The provided stream is not a valid Null "
                            + "compressed stream (invalid type 0x" + type.ToString("X") + ")");
            byte[] sizeBytes = new byte[3];
            instream.Read(sizeBytes, 0, 3);
            int decompressedSize = IOUtils.ToNDSu24(sizeBytes, 0);
            readBytes += 4;
            if (decompressedSize == 0)
            {
                sizeBytes = new byte[4];
                instream.Read(sizeBytes, 0, 4);
                decompressedSize = IOUtils.ToNDSs32(sizeBytes, 0);
                readBytes += 4;
            }

            byte[] data = new byte[decompressedSize];
            int readLength = instream.Read(data, 0, decompressedSize);
            outstream.Write(data, 0, readLength);
            if (readLength < decompressedSize)
                throw new NotEnoughDataException(readLength, decompressedSize);

            return readLength;
        }

        /// <summary>
        /// 'Compresses' the given input stream using the NULL format.
        /// </summary>
        public override int Compress(System.IO.Stream instream, long inLength, System.IO.Stream outstream)
        {
            if (inLength > 0xFFFFFFFF)
                throw new InputTooLargeException();

            long outSize = 4;

            outstream.WriteByte(0);
            if (inLength <= 0xFFFFFF)
            {
                outstream.WriteByte((byte)(inLength & 0xFF));
                outstream.WriteByte((byte)((inLength >> 8) & 0xFF));
                outstream.WriteByte((byte)((inLength >> 16) & 0xFF));
            }
            else
            {
                outstream.WriteByte(0);
                outstream.WriteByte(0);
                outstream.WriteByte(0);
                outstream.WriteByte((byte)(inLength & 0xFF));
                outstream.WriteByte((byte)((inLength >> 8) & 0xFF));
                outstream.WriteByte((byte)((inLength >> 16) & 0xFF));
                outstream.WriteByte((byte)((inLength >> 24) & 0xFF));
                outSize = 8;
            }

            byte[] buffer = new byte[Math.Min(int.MaxValue, inLength)];
            long remaining = inLength;
            while (remaining > 0)
            {
                int readLength = instream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (readLength == 0)
                    throw new StreamTooShortException();
                remaining -= readLength;
                outstream.Write(buffer, 0, readLength);
                outSize += readLength;
            }

            return (int)Math.Min(int.MaxValue, outSize);
        }
    }
}
