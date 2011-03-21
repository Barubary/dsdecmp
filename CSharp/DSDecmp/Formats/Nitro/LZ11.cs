using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DSDecmp.Formats.Nitro
{
    public class LZ11 : NitroCFormat
    {
        public LZ11() : base(0x11) { }

        public override void Decompress(Stream instream, long inLength, Stream outstream)
        {
            /*  Data header (32bit)
                  Bit 0-3   Reserved
                  Bit 4-7   Compressed type (must be 1 for LZ77)
                  Bit 8-31  Size of decompressed data. if 0, the next 4 bytes are decompressed length
                Repeat below. Each Flag Byte followed by eight Blocks.
                Flag data (8bit)
                  Bit 0-7   Type Flags for next 8 Blocks, MSB first
                Block Type 0 - Uncompressed - Copy 1 Byte from Source to Dest
                  Bit 0-7   One data byte to be copied to dest
                Block Type 1 - Compressed - Copy LEN Bytes from Dest-Disp-1 to Dest
                    If Reserved is 0: - Default
                      Bit 0-3   Disp MSBs
                      Bit 4-7   LEN - 3
                      Bit 8-15  Disp LSBs
                    If Reserved is 1: - Higher compression rates for files with (lots of) long repetitions
                      Bit 4-7   Indicator
                        If Indicator > 1:
                            Bit 0-3    Disp MSBs
                            Bit 4-7    LEN - 1 (same bits as Indicator)
                            Bit 8-15   Disp LSBs
                        If Indicator is 1:
                            Bit 0-3 and 8-19   LEN - 0x111
                            Bit 20-31          Disp
                        If Indicator is 0:
                            Bit 0-3 and 8-11   LEN - 0x11
                            Bit 12-23          Disp
                      
             */

            throw new NotImplementedException();
        }

        public override void Compress(Stream instream, long inLength, Stream outstream)
        {
            throw new NotImplementedException();
        }
    }
}
