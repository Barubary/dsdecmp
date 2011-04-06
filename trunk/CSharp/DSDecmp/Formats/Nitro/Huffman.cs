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

        public override bool Supports(System.IO.Stream stream, long inLength)
        {
            base.magicByte = (byte)BlockSize.FOURBIT;
            if (base.Supports(stream, inLength))
                return true;
            base.magicByte = (byte)BlockSize.EIGHTBIT;
            return base.Supports(stream, inLength);
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
                            + "compressed stream (invalid type 0x" + type.ToString("X") + "); unknown block size.");
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

            #region Read the Huff-tree

            if (readBytes >= inLength)
                throw new NotEnoughDataException(0, decompressedSize);
            int treeSize = instream.ReadByte(); readBytes++;
            if (treeSize < 0)
                throw new InvalidDataException("The stream is too short to contain a Huffman tree.");

            treeSize = (treeSize + 1) * 2;
            
            if (readBytes + treeSize >= inLength)
                throw new InvalidDataException("The Huffman tree is too large for the given input stream.");

            long treeEnd = (instream.Position - 1) + treeSize;

            // the relative offset may be 4 more (when the initial decompressed size is 0), but
            // since it's relative that doesn't matter, especially when it only matters if
            // the given value is odd or even.
            HuffTreeNode rootNode = new HuffTreeNode(instream, false, 5, treeEnd);

            readBytes += treeSize;
            // re-position the stream after the tree (the stream is currently positioned after the root
            // node, which is located at the start of the tree definition)
            instream.Position = treeEnd;

            #endregion

            // the current u32 we are reading bits from.
            uint data = 0;
            // the amount of bits left to read from <data>
            byte bitsLeft = 0;

            // a cache used for writing when the block size is four bits
            int cachedByte = -1;

            // the current output size
            int currentSize = 0;
            HuffTreeNode currentNode = rootNode;
            byte[] buffer = new byte[4];

            while (currentSize < decompressedSize)
            {
                #region find the next reference to a data node
                while (!currentNode.IsData)
                {
                    // if there are no bits left to read in the data, get a new byte from the input
                    if (bitsLeft == 0)
                    {
                        if (readBytes >= inLength)
                            throw new NotEnoughDataException(currentSize, decompressedSize);
                        int nRead = instream.Read(buffer, 0, 4);
                        if (nRead < 4)
                            throw new StreamTooShortException();
                        readBytes += nRead;
                        data = BitConverter.ToUInt32(buffer, 0);
                        bitsLeft = 32;
                    }
                    // get the next bit
                    bitsLeft--;
                    bool nextIsOne = (data & (1 << bitsLeft)) > 0;
                    // go to the next node, the direction of the child depending on the value of the current/next bit
                    currentNode = nextIsOne ? currentNode.Child1 : currentNode.Child0;
                }
                #endregion

                #region write the data in the current node (when possible)
                switch (blockSize)
                {
                    case BlockSize.EIGHTBIT:
                        {
                            // just copy the data if the block size is a full byte
                            outstream.WriteByte(currentNode.Data);
                            currentSize++;
                            break;
                        }
                    case BlockSize.FOURBIT:
                        {
                            // cache the first half of the data if the block size is a half byte
                            if (cachedByte < 0)
                            {
                                cachedByte = currentNode.Data << 4;
                            }
                            else
                            {
                                // if we already cached a half-byte, combine the two halves and write the full byte.
                                cachedByte |= currentNode.Data;
                                outstream.WriteByte((byte)cachedByte);
                                currentSize++;
                                // be sure to forget the two written half-bytes
                                cachedByte = -1;
                            }
                            break;
                        }
                    default:
                        throw new Exception("Unknown block size " + blockSize.ToString());
                }
                #endregion

                // make sure to start over next round
                currentNode = rootNode;
            }

            // the data is 4-byte aligned. Although very unlikely in this case (compressed bit blocks
            // are always 4 bytes long, and the tree size is generally 4-byte aligned as well),
            // skip any padding due to alignment.
            if (readBytes % 4 != 0)
                readBytes += 4 - (readBytes % 4);

            if (readBytes < inLength)
                throw new TooMuchInputException(readBytes, inLength);
        }

        public override int Compress(Stream instream, long inLength, Stream outstream)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// A single node in a Huffman tree.
        /// </summary>
        public class HuffTreeNode
        {
            /// <summary>
            /// The data contained in this node. May not mean anything when <code>isData == false</code>
            /// </summary>
            private byte data;
            /// <summary>
            /// A flag indicating if this node has been filled.
            /// </summary>
            private bool isFilled;
            /// <summary>
            /// The data contained in this node. May not mean anything when <code>isData == false</code>.
            /// Throws a NullReferenceException when this node has not been defined (ie: reference was outside the
            /// bounds of the tree definition)
            /// </summary>
            public byte Data
            {
                get
                {
                    if (!this.isFilled) throw new NullReferenceException("Reference to an undefined node in the huffman tree.");
                    return this.data;
                }
            }
            /// <summary>
            /// A flag indicating if this node contains data. If not, this is not a leaf node.
            /// </summary>
            private bool isData;
            /// <summary>
            /// Returns true if this node represents data.
            /// </summary>
            public bool IsData { get { return this.isData; } }

            /// <summary>
            /// The child of this node at side 0
            /// </summary>
            private HuffTreeNode child0;
            /// <summary>
            /// The child of this node at side 0
            /// </summary>
            public HuffTreeNode Child0 { get { return this.child0; } }
            /// <summary>
            /// The child of this node at side 1
            /// </summary>
            private HuffTreeNode child1;
            /// <summary>
            /// The child of this node at side 1
            /// </summary>
            public HuffTreeNode Child1 { get { return this.child1; } }

            /// <summary>
            /// Creates a new node in the Huffman tree.
            /// </summary>
            /// <param name="stream">The stream to read from. It is assumed that there is (at least)
            /// one more byte available to read.</param>
            /// <param name="isData">If this node is a data-node.</param>
            /// <param name="relOffset">The offset of this node in the source data, relative to the start
            /// of the compressed file.</param>
            /// <param name="maxStreamPos">The indicated end of the huffman tree. If the stream is past
            /// this position, the tree is invalid.</param>
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

                if (stream.Position >= maxStreamPos)
                {
                    // this happens when part of the tree is unused.
                    this.isFilled = false;
                    return;
                }
                this.isFilled = true;
                int readData = stream.ReadByte();
                if (readData < 0)
                    throw new StreamTooShortException();
                this.data = (byte)readData;

                this.isData = isData;

                if (!this.isData)
                {
                    int offset = this.data & 0x3F;
                    bool zeroIsData = (this.data & 0x80) > 0;
                    bool oneIsData = (this.data & 0x40) > 0;

                    // off AND NOT 1 == off XOR (off AND 1)
                    long zeroRelOffset = (relOffset ^ (relOffset & 1)) + offset * 2 + 2;

                    long currStreamPos = stream.Position;
                    // position the stream right before the 0-node
                    stream.Position += (zeroRelOffset - relOffset) - 1;
                    // read the 0-node
                    this.child0 = new HuffTreeNode(stream, zeroIsData, zeroRelOffset, maxStreamPos);
                    // the 1-node is directly behind the 0-node
                    this.child1 = new HuffTreeNode(stream, oneIsData, zeroRelOffset + 1, maxStreamPos);

                    // reset the stream position to right behind this node's data
                    stream.Position = currStreamPos;
                }
            }

            public override string ToString()
            {
                if (this.isData)
                {
                    return "<" + this.data.ToString("X2") + ">";
                }
                else
                {
                    return "[" + this.child0.ToString() + "," + this.child1.ToString() + "]";
                }
            }
            
        }
    }
}
