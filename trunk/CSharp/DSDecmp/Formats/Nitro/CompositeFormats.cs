using System;
using System.Collections.Generic;
using System.Text;

namespace DSDecmp.Formats.Nitro
{
    public class CompositeGBAFormat : CompositeFormat
    {
        public CompositeGBAFormat()
            : base(new Huffman4(), new Huffman8(), new LZ10()) { }

        public override string ShortFormatString
        {
            get { return "GBA"; }
        }

        public override string Description
        {
            get { return "All formats natively supported by the GBA."; }
        }

        public override bool SupportsCompression
        {
            get { return true; }
        }

        public override string CompressionFlag
        {
            get { return "gba*"; }
        }
    }

    public class CompositeNDSFormat : CompositeFormat
    {
        public CompositeNDSFormat()
            : base(new Huffman4(), new Huffman8(), new LZ10(), new LZ11()) { }

        public override string ShortFormatString
        {
            get { return "NDS"; }
        }

        public override string Description
        {
            get { return "All formats natively supported by the NDS."; }
        }

        public override bool SupportsCompression
        {
            get { return true; }
        }

        public override string CompressionFlag
        {
            get { return "nds*"; }
        }
    }
}
