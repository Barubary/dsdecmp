namespace DSDecmp.Formats.Nitro
{
    /// <summary>
    /// A composite format with all formats supported natively by the GBA.
    /// </summary>
    public class CompositeGBAFormat : CompositeFormat
    {
        /// <summary>
        /// Creates a new instance of the format composed of all native GBA compression formats.
        /// </summary>
        public CompositeGBAFormat()
            : base(new Huffman4(), new Huffman8(), new LZ10())
        {
        }

        /// <summary>
        /// Gets a short string identifying this compression format.
        /// </summary>
        public override string ShortFormatString => "GBA";

        /// <summary>
        /// Gets a short description of this compression format (used in the program usage).
        /// </summary>
        public override string Description => "All formats natively supported by the GBA.";

        /// <summary>
        /// Gets if this format supports compressing a file.
        /// </summary>
        public override bool SupportsCompression => true;

        /// <summary>
        /// Gets the value that must be given on the command line in order to compress using this format.
        /// </summary>
        public override string CompressionFlag => "gba*";
    }

    /// <summary>
    /// A composite format with all formats supported natively by the NDS (but not LZ-Overlay)
    /// </summary>
    public class CompositeNDSFormat : CompositeFormat
    {
        /// <summary>
        /// Creates a new instance of the format composed of all native NDS compression formats.
        /// </summary>
        public CompositeNDSFormat()
            : base(new Huffman4(), new Huffman8(), new LZ10(), new LZ11())
        {
        }

        /// <summary>
        /// Gets a short string identifying this compression format.
        /// </summary>
        public override string ShortFormatString => "NDS";

        /// <summary>
        /// Gets a short description of this compression format (used in the program usage).
        /// </summary>
        public override string Description => "All formats natively supported by the NDS.";

        /// <summary>
        /// Gets if this format supports compressing a file.
        /// </summary>
        public override bool SupportsCompression => true;

        /// <summary>
        /// Gets the value that must be given on the command line in order to compress using this format.
        /// </summary>
        public override string CompressionFlag => "nds*";
    }
}
