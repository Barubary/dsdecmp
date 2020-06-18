using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using DSDecmp;

namespace GameFormats
{
    /// <summary>
    /// Compressor/decompressor for the LZE format found in Luminous Arc games. Format specification by Roger Pepitone; http://pastebin.com/qNgSB2f9
    /// </summary>
    public class LuminousArc : CompressionFormat
    {

        public override string ShortFormatString
        {
            get { return "LZE/Le"; }
        }

        public override string Description
        {
            get { return "A variant of an LZ/RLE scheme found in Luminous Arc games."; }
        }

        public override string CompressionFlag
        {
            get { return "Le"; }
        }

        public override bool SupportsCompression
        {
            get { return true; }
        }

        private static bool lookAhead = false;
        /// <summary>
        /// Gets or sets if, when compressing using this format, the optimal compression scheme should be used.
        /// </summary>
        public static bool LookAhead { get { return lookAhead; } set { lookAhead = value; } }

        /*
         * An LZE / Le file consists of the following:
            - A six byte header
            - A series of blocks

            The header consists of:
            - 2 bytes: 0x4c, 0x65 ("Le" in ASCII)
            - 4 bytes: the size of the uncompressed data in little-endian format

            Each block consists of:
            - 1 byte: the types for the following mini-records
                  (2 bits per type, stored with the first type at the least
                  significant bit)
            - 4 mini-records


            Each mini-record consists of:
            - If its type is 0:
            -- 2 bytes BYTE1 BYTE2: Write (3 + (BYTE2 >> 4)) bytes from
                                    back (5 + (BYTE1 | ((BYTE2 & 0xf) << 8))) to output
            - If its type is 1:
            -- 1 byte BYTE1:  Write (2 + (BYTE >> 2)) bytes from 
                              back (1 + (BYTE & 3)) to output
            - If its type is 2:
            -- 1 byte: (copied to output stream)
            - If its type is 3:
            -- 3 bytes: (copied to output stream)


            The last block may go over the end; if so, ignore any excess data.
         */

        #region Method: Supports(Stream, inLength)
        /// <summary>
        /// Determines if this format may potentially be used to decompress the given stream.
        /// Does not guarantee success when returning true, but does guarantee failure when returning false.
        /// </summary>
        public override bool Supports(System.IO.Stream stream, long inLength)
        {
            long streamStart = stream.Position;
            try
            {
                if (inLength <= 6) // min 6 byte header
                    return false;

                byte[] header = new byte[2];
                stream.Read(header, 0, 2);
                if (header[0] != 'L' || header[1] != 'e')
                    return false;

                byte[] outLength = new byte[4];
                stream.Read(outLength, 0, 4);
                if (IOUtils.ToNDSu32(outLength, 0) == 0)
                    return inLength == 6;

                // as long as the magic is OK, anything else is fine for now. (for this superficial check)
                return true;
            }
            finally
            {
                stream.Position = streamStart;
            }
        }
        #endregion

        #region Method: Decompress(instream, inLength, outstream)
        /// <summary>
        /// Decompresses the given stream using the LZE/Le compression format.
        /// </summary>
        public override long Decompress(System.IO.Stream instream, long inLength, System.IO.Stream outstream)
        {
            long readBytes = 0;

            byte[] magic = new byte[2];
            instream.Read(magic, 0, 2);
            if (magic[0] != 'L' || magic[1] != 'e')
                throw new InvalidDataException("The provided stream is not a valid LZE (Le) "
                            + "compressed stream (invalid magic '" + (char)magic[0] + (char)magic[1] + "')");
            byte[] sizeBytes = new byte[4];
            instream.Read(sizeBytes, 0, 4);
            uint decompressedSize = IOUtils.ToNDSu32(sizeBytes, 0);
            readBytes += 4;

            // the maximum 'DISP-5' is 0xFFF.
            int bufferLength = 0xFFF + 5;
            byte[] buffer = new byte[bufferLength];
            int bufferOffset = 0;


            int currentOutSize = 0;
            int flags = 0, mask = 3;
            while (currentOutSize < decompressedSize)
            {
                // (throws when requested new flags byte is not available)
                #region Update the mask. If all flag bits have been read, get a new set.
                // the current mask is the mask used in the previous run. So if it masks the
                // last flag bit, get a new flags byte.
                if (mask == 3)
                {
                    if (readBytes >= inLength)
                        throw new NotEnoughDataException(currentOutSize, decompressedSize);
                    flags = instream.ReadByte(); readBytes++;
                    if (flags < 0)
                        throw new StreamTooShortException();
                    mask = 0xC0;
                }
                else
                {
                    mask >>= 2;
                    flags >>= 2;
                }
                #endregion

                switch (flags & 0x3)
                {
                    case 0:
                        #region 0 -> LZ10-like format
                        {
                            #region Get length and displacement('disp') values from next 2 bytes
                            // there are < 2 bytes available when the end is at most 1 byte away
                            if (readBytes + 1 >= inLength)
                            {
                                // make sure the stream is at the end
                                if (readBytes < inLength)
                                {
                                    instream.ReadByte(); readBytes++;
                                }
                                throw new NotEnoughDataException(currentOutSize, decompressedSize);
                            }
                            int byte1 = instream.ReadByte(); readBytes++;
                            int byte2 = instream.ReadByte(); readBytes++;
                            if (byte2 < 0)
                                throw new StreamTooShortException();

                            // the number of bytes to copy
                            int length = byte2 >> 4;
                            length += 3;

                            // from where the bytes should be copied (relatively)
                            int disp = ((byte2 & 0x0F) << 8) | byte1;
                            disp += 5;

                            if (disp > currentOutSize)
                                throw new InvalidDataException("Cannot go back more than already written. "
                                        + "DISP = 0x" + disp.ToString("X") + ", #written bytes = 0x" + currentOutSize.ToString("X")
                                        + " at 0x" + (instream.Position - 2).ToString("X"));
                            #endregion

                            int bufIdx = bufferOffset + bufferLength - disp;
                            for (int i = 0; i < length; i++)
                            {
                                byte next = buffer[bufIdx % bufferLength];
                                bufIdx++;
                                outstream.WriteByte(next);
                                buffer[bufferOffset] = next;
                                bufferOffset = (bufferOffset + 1) % bufferLength;
                            }
                            currentOutSize += length;

                            break;
                        }
                        #endregion
                    case 1:
                        #region 1 -> compact LZ10/RLE-like format
                        {
                            #region Get length and displacement('disp') values from next byte
                            // there are < 2 bytes available when the end is at most 1 byte away
                            if (readBytes >= inLength)
                            {
                                throw new NotEnoughDataException(currentOutSize, decompressedSize);
                            }
                            int b = instream.ReadByte(); readBytes++;
                            if (b < 0)
                                throw new StreamTooShortException();

                            // the number of bytes to copy
                            int length = b >> 2;
                            length += 2;

                            // from where the bytes should be copied (relatively)
                            int disp = (b & 0x03);
                            disp += 1;

                            if (disp > currentOutSize)
                                throw new InvalidDataException("Cannot go back more than already written. "
                                        + "DISP = 0x" + disp.ToString("X") + ", #written bytes = 0x" + currentOutSize.ToString("X")
                                        + " at 0x" + (instream.Position - 1).ToString("X"));
                            #endregion

                            int bufIdx = bufferOffset + bufferLength - disp;
                            for (int i = 0; i < length; i++)
                            {
                                byte next = buffer[bufIdx % bufferLength];
                                bufIdx++;
                                outstream.WriteByte(next);
                                buffer[bufferOffset] = next;
                                bufferOffset = (bufferOffset + 1) % bufferLength;
                            }
                            currentOutSize += length;
                            break;
                        }
                        #endregion
                    case 2:
                        #region 2 -> copy 1 byte
                        {
                            if (readBytes >= inLength)
                                throw new NotEnoughDataException(currentOutSize, decompressedSize);
                            int next = instream.ReadByte(); readBytes++;
                            if (next < 0)
                                throw new StreamTooShortException();

                            currentOutSize++;
                            outstream.WriteByte((byte)next);
                            buffer[bufferOffset] = (byte)next;
                            bufferOffset = (bufferOffset + 1) % bufferLength;
                            break;
                        }
                        #endregion
                    case 3:
                        #region 3 -> copy 3 bytes
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                if (readBytes >= inLength)
                                    throw new NotEnoughDataException(currentOutSize, decompressedSize);
                                int next = instream.ReadByte(); readBytes++;
                                if (next < 0)
                                    throw new StreamTooShortException();

                                currentOutSize++;
                                outstream.WriteByte((byte)next);
                                buffer[bufferOffset] = (byte)next;
                                bufferOffset = (bufferOffset + 1) % bufferLength;
                            }
                            break;
                        }
                    #endregion
                    default:
                        throw new Exception("BUG: Mask is not 2 bits long!");
                }

                outstream.Flush();
            }

            if (readBytes < inLength)
            {
                // the input may be 4-byte aligned.
                if ((readBytes ^ (readBytes & 3)) + 4 < inLength)
                    throw new TooMuchInputException(readBytes, inLength);
                // (this happens rather often for Le files?)
            }

            return decompressedSize;
        }
        #endregion

        /// <summary>
        /// Checks if the given aguments have the '-opt' option, which makes this format
        /// compress using (near-)optimal compression instead of the original compression algorithm.
        /// </summary>
        public override int ParseCompressionOptions(string[] args)
        {
            LookAhead = false;
            if (args.Length > 0)
                if (args[0] == "-opt")
                {
                    LookAhead = true;
                    return 1;
                }
            return 0;
        }

        public unsafe override int Compress(System.IO.Stream instream, long inLength, System.IO.Stream outstream)
        {
            // block type 0: stores at most 3+0xF  = 0x12 = 18 bytes (in 2 bytes)
            // block type 1: stores at most 2+0x3F = 0x41 = 65 bytes (in 1 byte)
            // block type 2: 1 raw byte
            // block type 3: 3 raw bytes

            if (LookAhead)
                return CompressWithLA(instream, inLength, outstream);


            // save the input data in an array to prevent having to go back and forth in a file
            byte[] indata = new byte[inLength];
            int numReadBytes = instream.Read(indata, 0, (int)inLength);
            if (numReadBytes != inLength)
                throw new StreamTooShortException();

            // write the compression head first
            outstream.WriteByte((byte)'L');
            outstream.WriteByte((byte)'e');
            outstream.WriteByte((byte)(inLength & 0xFF));
            outstream.WriteByte((byte)((inLength >> 8) & 0xFF));
            outstream.WriteByte((byte)((inLength >> 16) & 0xFF));
            outstream.WriteByte((byte)((inLength >> 24) & 0xFF));

            int compressedLength = 6;

            fixed (byte* instart = &indata[0])
            {
                // we do need to buffer the output, as the first byte indicates which blocks are compressed.
                // this version does not use a look-ahead, so we do not need to buffer more than 4 blocks at a time.
                // (a block is at most 3 bytes long)
                byte[] outbuffer = new byte[4 * 3 + 1];
                outbuffer[0] = 0;
                int bufferlength = 1, bufferedBlocks = 0;
                int readBytes = 0;

                int cacheByte = -1;

                while (readBytes < inLength)
                {
                    #region If 4 blocks are bufferd, write them and reset the buffer
                    // we can only buffer 4 blocks at a time.
                    if (bufferedBlocks == 4)
                    {
                        outstream.Write(outbuffer, 0, bufferlength);
                        compressedLength += bufferlength;
                        // reset the buffer
                        outbuffer[0] = 0;
                        bufferlength = 1;
                        bufferedBlocks = 0;
                    }
                    #endregion

                    // type 0: 3 <= len <= 18; 5 <= disp <= 0x1004
                    // type 1: 2 <= len <= 65; 1 <= disp <= 4
                    // type 2: 1 raw byte
                    // type 3: 3 raw bytes

                    // check if we can compress it using type 1 first (only 1 byte-long block)
                    int disp;
                    int oldLength = Math.Min(readBytes, 0x1004);
                    int length = LZUtil.GetOccurrenceLength(instart + readBytes, (int)Math.Min(inLength - readBytes, 65),
                                                            instart + readBytes - oldLength, oldLength, out disp, 1);
                    if (disp >= 1 && ((disp <= 4 && length >= 2) || (disp >= 5 && length >= 3)))
                    {
                        if (cacheByte >= 0)
                        {
                            // write a single raw byte block (the previous byte could not be the start of any compressed block)
                            outbuffer[bufferlength++] = (byte)(cacheByte & 0xFF);
                            outbuffer[0] |= (byte)(2 << (bufferedBlocks * 2));
                            cacheByte = -1;
                            bufferedBlocks++;
                            // the block set may be full; just retry this iteration.
                            continue;
                        }
                        if (disp >= 5)
                        {
                            #region compress using type 0

                            // type 0: store len/disp in 2 bytes:
                            // AB CD, with len = C + 3, disp = DAB + 5

                            // make sure we do not try to compress more than fits into the block
                            length = Math.Min(length, 0xF + 3);

                            readBytes += length;

                            outbuffer[bufferlength++] = (byte)((disp - 5) & 0xFF);
                            outbuffer[bufferlength] = (byte)(((disp - 5) >> 8) & 0xF);
                            outbuffer[bufferlength++] |= (byte)(((length - 3) & 0xF) << 4);

                            #endregion
                        }
                        else // 1 <= disp <= 4
                        {
                            #region compress using type 1

                            // type 1: store len/disp in 1 byte:
                            // ABCDEFGH, wih len = ABCDEF + 2, disp = GH + 1

                            readBytes += length;

                            outbuffer[bufferlength] = (byte)(((length - 2) << 2) & 0xFC);
                            outbuffer[bufferlength] |= (byte)((disp - 1) & 0x3);
                            bufferlength++;

                            outbuffer[0] |= (byte)(1 << (bufferedBlocks * 2));

                            #endregion
                        }
                    }
                    else
                    {
                        if (cacheByte < 0)
                        {
                            // first fail? remember byte, try to compress starting at next byte
                            cacheByte = *(instart + (readBytes++));
                            continue;
                        }
                        else
                        {
                            // 2 consecutive fails -> store 3 raw bytes (type 3) if possible.
                            if (inLength - readBytes >= 2)
                            {
                                outbuffer[bufferlength++] = (byte)(cacheByte & 0xFF);
                                outbuffer[bufferlength++] = *(instart + (readBytes++));
                                outbuffer[bufferlength++] = *(instart + (readBytes++));
                                outbuffer[0] |= (byte)(3 << (bufferedBlocks * 2));
                                cacheByte = -1;
                            }
                            else
                            {
                                // there are only two bytes remaining (incl. the cched byte)
                                // so write the cached byte first as single raw byte.
                                // keep the next/last byte as new cache, since the block buffer may be full.
                                outbuffer[bufferlength++] = (byte)(cacheByte & 0xFF);
                                outbuffer[0] |= (byte)(2 << (bufferedBlocks * 2));
                                cacheByte = *(instart + (readBytes++));
                            }
                        }
                    }
                    
                    bufferedBlocks++;
                }

                // there may be one cache-byte left.
                if (cacheByte >= 0)
                {
                    // if the current set of blocks is full, empty it first
                    if (bufferedBlocks == 4)
                    {
                        #region empty block buffer

                        outstream.Write(outbuffer, 0, bufferlength);
                        compressedLength += bufferlength;
                        // reset the buffer
                        outbuffer[0] = 0;
                        bufferlength = 1;
                        bufferedBlocks = 0;

                        #endregion
                    }

                    outbuffer[bufferlength++] = (byte)(cacheByte & 0xFF);
                    cacheByte = -1;
                    outbuffer[0] |= (byte)(2 << (bufferedBlocks * 2));
                    bufferedBlocks++;
                }

                // copy any remaining blocks to the output
                if (bufferedBlocks > 0)
                {
                    outstream.Write(outbuffer, 0, bufferlength);
                    compressedLength += bufferlength;
                }
            }

            return compressedLength;
        }

        private unsafe int CompressWithLA(Stream instream, long inLength, Stream outstream)
        {
            throw new NotImplementedException();
        }
    }
}
