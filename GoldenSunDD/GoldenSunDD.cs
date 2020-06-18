using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using DSDecmp;

namespace GameFormats
{
    public class GoldenSunDD : CompressionFormat
    {

        public override string ShortFormatString
        {
            get { return "GSDD"; }
        }

        public override string Description
        {
            get { return "A variant of the LZ-0x11 scheme found in Golden Sun: Dark Dawn."; }
        }

        public override string CompressionFlag
        {
            get { return "gsdd"; }
        }

        public override bool SupportsCompression
        {
            get { return false; }
        }

        public override bool Supports(System.IO.Stream stream, long inLength)
        {
            long streamStart = stream.Position;
            try
            {
                // because of the specific format, and especially since it overlaps with
                // the LZH8 header format, we'll need to try and decompress the file in
                // order to check if it is supported.
                try
                {
                    using (MemoryStream tempStream = new MemoryStream())
                        this.Decompress(stream, inLength, tempStream);
                    return true;
                }
                catch (TooMuchInputException)
                {
                    // too much input is still OK.
                    return true;
                }
                catch
                {
                    // anything else is not OK.
                    return false;
                }
            }
            finally
            {
                stream.Position = streamStart;
            }
        }

        #region Decompression method
        public override long Decompress(System.IO.Stream instream, long inLength, System.IO.Stream outstream)
        {
            #region format specification
            // no NDSTEK-like specification for this one; I seem to not be able to get those right.
            /*
             * byte tag; // 0x40
             * byte[3] decompressedSize;
             * the rest is the data;
             * 
             * for each chunk:
             *      - first byte determines which blocks are compressed
             *           multiply by -1 to get the proper flags (1->compressed, 0->raw)
             *      - then come 8 blocks:
             *          - a non-compressed block is simply one single byte
             *          - a compressed block can have 3 sizes:
             *              - A0 CD EF
             *                  -> Length = EF + 0x10, Disp = CDA
             *              - A1 CD EF GH
             *                  -> Length = GHEF + 0x110, Disp = CDA
             *              - AB CD  (B > 1)
             *                  -> Length = B, Disp = CDA
             *              Copy <Length> bytes from Dest-<Disp> to Dest (with <Dest> similar to the NDSTEK specs)
             */
            #endregion

            long readBytes = 0;

            byte type = (byte)instream.ReadByte();
            if (type != 0x40)
                throw new InvalidDataException("The provided stream is not a valid 'LZ-0x40' "
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

            // the maximum 'DISP' is 0xFFF.
            int bufferLength = 0x1000;
            byte[] buffer = new byte[bufferLength];
            int bufferOffset = 0;

            int currentOutSize = 0;
            int currentBlock = 0;
            // the expended flag byte
            bool[] expandedFlags = null;
            while (currentOutSize < decompressedSize)
            {
                // (throws when requested new flags byte is not available)
                #region Update the mask. If all flag bits have been read, get a new set.
                // the current mask is the mask used in the previous run. So if it masks the
                // last flag bit, get a new flags byte.
                if (currentBlock == 8)
                {
                    if (readBytes >= inLength)
                        throw new NotEnoughDataException(currentOutSize, decompressedSize);
                    int flags = instream.ReadByte(); readBytes++;
                    if (flags < 0)
                        throw new StreamTooShortException();

                    // determine which blocks are compressed
                    int b = 0;
                    expandedFlags = new bool[8];
                    // flags = -flags
                    while (flags > 0)
                    {
                        bool bit = (flags & 0x80) > 0;
                        flags = (flags & 0x7F) << 1;
                        expandedFlags[b++] = (flags == 0) || !bit;
                    }

                    currentBlock = 0;
                }
                else
                {
                    currentBlock++;
                }
                #endregion

                // bit = 1 <=> compressed.
                if (expandedFlags[currentBlock])
                {
                    // (throws when < 2, 3 or 4 bytes are available)
                    #region Get length and displacement('disp') values from next 2, 3 or 4 bytes

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

                    int disp, length;
                    disp = (byte1 >> 4) + (byte2 << 4);
                    if (disp > currentOutSize)
                        throw new InvalidDataException("Cannot go back more than already written. "
                                + "DISP = 0x" + disp.ToString("X") + ", #written bytes = 0x" + currentOutSize.ToString("X")
                                + " at 0x" + (instream.Position - 2).ToString("X"));

                    switch (byte1 & 0x0F)
                    {
                        case 0:
                            {
                                if (readBytes >= inLength)
                                    throw new NotEnoughDataException(currentOutSize, decompressedSize);
                                int byte3 = instream.ReadByte(); readBytes++;
                                if (byte3 < 0)
                                    throw new StreamTooShortException();
                                length = byte3 + 0x10;
                                break;
                            }
                        case 1:
                            {
                                if (readBytes + 1 >= inLength)
                                    throw new NotEnoughDataException(currentOutSize, decompressedSize);
                                int byte3 = instream.ReadByte(); readBytes++;
                                int byte4 = instream.ReadByte(); readBytes++;
                                if (byte4 < 0)
                                    throw new StreamTooShortException();
                                length = ((byte3 << 8) + byte4) + 0x110;
                                break;
                            }
                        default:
                            {
                                length = byte1 & 0x0F;
                                break;
                            }
                    }
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
                }
                else
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
                outstream.Flush();
            }

            if (readBytes < inLength)
            {
                // the input may be 4-byte aligned.
                if ((readBytes ^ (readBytes & 3)) + 4 < inLength)
                    throw new TooMuchInputException(readBytes, inLength);
            }

            return decompressedSize;
        }
        #endregion

        public override int Compress(System.IO.Stream instream, long inLength, System.IO.Stream outstream)
        {
            throw new NotImplementedException();
        }
    }
}
