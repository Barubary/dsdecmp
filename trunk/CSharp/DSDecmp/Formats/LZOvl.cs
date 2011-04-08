using System;
using System.Collections.Generic;
using System.Text;

namespace DSDecmp.Formats
{
    /// <summary>
    /// The LZ-Overlay compression format. Compresses part of the file from end to start.
    /// Is used for the 'overlay' files in NDS games, as well as arm9.bin
    /// </summary>
    public class LZOvl : CompressionFormat
    {
        #region Method: Supports(Stream, long)
        public override bool Supports(System.IO.Stream stream, long inLength)
        {
            // assume the 'inLength' does not include the 12 bytes at the end of arm9.bin

            // only allow integer-sized files
            if (inLength > 0xFFFFFFFFL)
                return false;
            // the header is 4 bytes minimum
            if (inLength < 4)
                return false;
            long streamStart = stream.Position;
            byte[] header = new byte[Math.Min(inLength, 0x20)];
            stream.Position += inLength - header.Length;
            stream.Read(header, 0, header.Length);
            // reset the stream
            stream.Position = streamStart;

            uint extraSize = IOUtils.ToNDSu32(header, header.Length - 4);
            if (extraSize == 0)
                return true;
            // if the extrasize is nonzero, the minimum header length is 8  bytes
            if (header.Length < 8)
                return false;
            byte headerLen = header[header.Length - 5];
            if (inLength < headerLen)
                return false;
            // the compressed length is assumed to be valid.
            // verify that the rest of the header is filled with 0xFF
            for (int i = header.Length - 9; i >= header.Length - headerLen; i--)
                if (header[i] != 0xFF)
                    return false;
            return true;
        }
        #endregion

        public override void Decompress(System.IO.Stream instream, long inLength, System.IO.Stream outstream)
        {
            // Overlay LZ compression is basically just LZ-0x10 compression.
            // however the order if reading is reversed: the compression starts at the end of the file.
            // Assuming we start reading at the end towards the beginning, the format is:
            /*
             * u32 extraSize; // decompressed data size = file length (including header) + this value
             * u8 headerSize;
             * u24 compressedLength; // can be less than file size (w/o header). If so, the rest of the file is uncompressed.
             * u8[headerSize-8] padding; // 0xFF-s
             * 
             * 0x10-like-compressed data follows (without the usual 4-byte header).
             * The only difference is that 2 should be added to the DISP value in compressed blocks
             * to get the proper value.
             * the u32 and u24 are read most significant byte first.
             * if extraSize is 0, there is no headerSize, decompressedLength or padding.
             * the data starts immediately, and is uncompressed.
             * 
             * arm9.bin has 3 extra u32 values at the 'start' (ie: end of the file),
             * which may be ignored. (and are ignored here) These 12 bytes also should not
             * be included in the computation of the output size.
             */
            throw new NotImplementedException();
        }

        public override int Compress(System.IO.Stream instream, long inLength, System.IO.Stream outstream)
        {
            throw new NotImplementedException();
        }
    }
}
