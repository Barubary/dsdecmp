using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DSDecmp.Formats.Nitro
{
    public class RLE : NitroCFormat
    {
        public RLE() : base(0x30) { }

        public override void Decompress(Stream instream, long inLength, Stream outstream)
        {
            /*      
                Data header (32bit)
                    Bit 0-3   Reserved
                    Bit 4-7   Compressed type (must be 3 for run-length)
                    Bit 8-31  Size of decompressed data
                Repeat below. Each Flag Byte followed by one or more Data Bytes.
                Flag data (8bit)
                    Bit 0-6   Expanded Data Length (uncompressed N-1, compressed N-3)
                    Bit 7     Flag (0=uncompressed, 1=compressed)
                Data Byte(s) - N uncompressed bytes, or 1 byte repeated N times
             */

            long readBytes = 0;

            byte type = (byte)instream.ReadByte();
            if (type != base.magicByte)
                throw new InvalidDataException("The provided stream is not a valid LZ-0x11 "
                            + "compressed stream (invalid type 0x" + type.ToString("X") + ")");
            byte[] sizeBytes = new byte[3];
            instream.Read(sizeBytes, 0, 3);
            int decompressedSize = base.Bytes2Size(sizeBytes);
            readBytes += 4;
            if (decompressedSize == 0)
            {
                sizeBytes = new byte[4];
                instream.Read(sizeBytes, 0, 4);
                decompressedSize = base.Bytes2Size(sizeBytes);
                readBytes += 4;
            }


            int currentOutSize = 0;
            while (currentOutSize < decompressedSize)
            {
                #region (try to) get the flag byte with the length data and compressed flag

                if (readBytes >= inLength)
                    throw new NotEnoughDataException(currentOutSize, decompressedSize);
                int flag = instream.ReadByte(); readBytes++;
                if (flag < 0)
                    throw new StreamTooShortException();

                bool compressed = (flag & 0x80) > 0;
                int length = flag & 0x7F;

                if (compressed)
                    length += 3;
                else
                    length += 1;

                #endregion

                if (compressed)
                {
                    #region compressed: write the next byte (length) times.

                    if (readBytes >= inLength)
                        throw new NotEnoughDataException(currentOutSize, decompressedSize);
                    int data = instream.ReadByte(); readBytes++;
                    if (data < 0)
                        throw new StreamTooShortException();

                    if (currentOutSize + length > decompressedSize)
                        throw new InvalidDataException("The given stream is not a valid RLE stream; the "
                            + "output length does not match the provided plaintext length.");
                    byte bdata = (byte)data;
                    for (int i = 0; i < length; i++)
                    {
                        // Stream.Write(byte[], offset, len) may also work, but only if it is a circular buffer
                        outstream.WriteByte(bdata);
                        currentOutSize++;
                    }

                    #endregion
                }
                else
                {
                    #region uncompressed: copy the next (length) bytes.

                    int tryReadLength = length;
                    // limit the amount of bytes read by the indicated number of bytes available
                    if (readBytes + length > inLength)
                        tryReadLength = (int)(inLength - readBytes);
                    
                    byte[] data = new byte[length];
                    int readLength = instream.Read(data, 0, (int)tryReadLength);
                    readBytes += readLength;
                    outstream.Write(data, 0, readLength);
                    currentOutSize += readLength;

                    // if the attempted number of bytes read is less than the desired number, the given input
                    // length is too small (or there is not enough data in the stream)
                    if (tryReadLength < length)
                        throw new NotEnoughDataException(currentOutSize, decompressedSize);
                    // if the actual number of read bytes is even less, it means that the end of the stream has
                    // bee reached, thus the given input length is larger than the actual length of the input
                    if (readLength < length)
                        throw new StreamTooShortException();

                    #endregion
                }
            }
        }

        public override void Compress(Stream instream, long inLength, Stream outstream)
        {
            throw new NotImplementedException();
        }
    }
}
