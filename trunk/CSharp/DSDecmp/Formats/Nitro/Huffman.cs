using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DSDecmp.Formats.Nitro
{
    /// <summary>
    /// Compressor and decompressor for the Huffman format used in many of the games for the
    /// newer Nintendo consoles and handhelds.
    /// </summary>
    public class Huffman : NitroCFormat
    {
        public enum BlockSize : byte { FOURBIT = 0x24, EIGHTBIT = 0x28 }

        public Huffman() : base(0) { }

        public override bool Supports(System.IO.Stream stream)
        {
            base.magicByte = (byte)BlockSize.FOURBIT;
            if (base.Supports(stream))
                return true;
            base.magicByte = (byte)BlockSize.EIGHTBIT;
            return base.Supports(stream);
        }

        public override void Decompress(Stream instream, long inLength, Stream outstream)
        {
            #region GBATEK format specification
            /*
                Data Header (32bit)
                    Bit0-3   Data size in bit units (normally 4 or 8)
                    Bit4-7   Compressed type (must be 2 for Huffman)
                    Bit8-31  24bit size of decompressed data in bytes
                Tree Size (8bit)
                    Bit0-7   Size of Tree Table/2-1 (ie. Offset to Compressed Bitstream)
                Tree Table (list of 8bit nodes, starting with the root node)
                    Root Node and Non-Data-Child Nodes are:
                    Bit0-5   Offset to next child node,
                            Next child node0 is at (CurrentAddr AND NOT 1)+Offset*2+2
                            Next child node1 is at (CurrentAddr AND NOT 1)+Offset*2+2+1
                    Bit6     Node1 End Flag (1=Next child node is data)
                    Bit7     Node0 End Flag (1=Next child node is data)
                    Data nodes are (when End Flag was set in parent node):
                    Bit0-7   Data (upper bits should be zero if Data Size is less than 8)
                Compressed Bitstream (stored in units of 32bits)
                    Bit0-31  Node Bits (Bit31=First Bit)  (0=Node0, 1=Node1)
            */
            #endregion

            long readBytes = 0;

            byte type = (byte)instream.ReadByte();
            BlockSize blockSize = BlockSize.FOURBIT;
            if (type != (byte)blockSize)
                blockSize = BlockSize.EIGHTBIT;
            if (type != (byte)blockSize)
                throw new InvalidDataException("The provided stream is not a valid Huffman "
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

            if (readBytes >= inLength)
                throw new NotEnoughDataException(0, decompressedSize);
            int treeSize = instream.ReadByte(); readBytes++;
            if (treeSize < 0)
                throw new InvalidDataException("The stream is too short to contain a Huffman tree.");

            treeSize = (treeSize * 2) + 1;
            if (readBytes + treeSize >= inLength)
                throw new InvalidDataException("The Huffman tree is too large for the given input stream.");

            // the relative offset may be 4 more (when the initial decompressed size is 0), but
            // since it's relative that doesn't matter, especially when it only matters if
            // the given value is odd or even.
            HuffTreeNode rootNode = new HuffTreeNode(instream, false, 5, instream.Position + treeSize);

            throw new NotImplementedException();
        }

        public override void Compress(Stream instream, long inLength, Stream outstream)
        {
            throw new NotImplementedException();
        }



        public class HuffTreeNode
        {
            /// <summary>
            /// The data contained in this node. May not mena anything when <code>isData == false</code>
            /// </summary>
            private byte data;
            /// <summary>
            /// A flag indicating if this ia a 'child1' of another node.
            /// </summary>
            private bool isOne;
            /// <summary>
            /// A flag indicating if this node contains data. If not, this is not a leaf node.
            /// </summary>
            private bool isData;

            /// <summary>
            /// The child of this node at side 0
            /// </summary>
            private HuffTreeNode child0;
            /// <summary>
            /// The child of this node at side 1
            /// </summary>
            private HuffTreeNode child1;

            /// <summary>
            /// Creates a new node in the Huffman tree.
            /// </summary>
            /// <param name="stream">The stream to read from. It is assumed that there is (at least)
            /// one more byte available to read.</param>
            /// <param name="isData">If this node is a data-node.</param>
            /// <param name="relOffset">The offset of this node in the source data, relative to the start
            /// of the compressed file.</param>
            public HuffTreeNode(Stream stream, bool isData, long relOffset, long maxStreamPos)
            {
                /*
                 Tree Table (list of 8bit nodes, starting with the root node)
                    Root Node and Non-Data-Child Nodes are:
                    Bit0-5   Offset to next child node,
                            Next child node0 is at (CurrentAddr AND NOT 1)+Offset*2+2
                            Next child node1 is at (CurrentAddr AND NOT 1)+Offset*2+2+1
                    Bit6     Node1 End Flag (1=Next child node is data)
                    Bit7     Node0 End Flag (1=Next child node is data)
                    Data nodes are (when End Flag was set in parent node):
                    Bit0-7   Data (upper bits should be zero if Data Size is less than 8)
                 */
                if (stream.Position <= maxStreamPos)
                    throw new InvalidDataException("The Huffman tree does not fit in the available number of bytes.");
                int readData = stream.ReadByte();
                if (readData < 0)
                    throw new StreamTooShortException();
                this.data = (byte)readData;

                this.isData = isData;

                if (!this.isData)
                {
                    int offset = this.data & 0x3F;
                    bool zeroIsData = (this.data & 0x40) > 0;
                    bool oneIsData = (this.data & 0x80) > 0;

                    // off AND NOT 1 == off XOR (off AND 1)
                    long zeroRelOffset = (relOffset ^ (relOffset & 1)) + offset * 2 + 2;

                    long currStreamPos = stream.Position;
                    // position the stream right before the 0-node
                    stream.Position += (zeroRelOffset - relOffset) - 1;
                    // read the 0-node
                    this.child0 = new HuffTreeNode(stream, zeroIsData, zeroRelOffset, maxStreamPos);
                    // the 1-node is dircetly behind the 0-node
                    this.child1 = new HuffTreeNode(stream, oneIsData, zeroRelOffset + 1, maxStreamPos);

                    // reset the stream position to right behind this node's data
                    stream.Position = currStreamPos;
                }
            }
            
        }
    }
}
