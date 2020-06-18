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
    public abstract class Huffman : NitroCFormat
    {
        #region Enum: BlockSize
        /// <summary>
        /// The possible data sizes used in Huffman compression formats on the GBA/NDS.
        /// </summary>
        public enum BlockSize : byte
        {
            /// <summary>
            /// Each data block is four bits long.
            /// </summary>
            FOURBIT = 0x24,
            /// <summary>
            /// Each data block is eight bits long.
            /// </summary>
            EIGHTBIT = 0x28
        }
        #endregion

        /// <summary>
        /// Sets the block size used when using the Huffman format to compress.
        /// </summary>
        public BlockSize CompressBlockSize { get; set; }

        /// <summary>
        /// Gets if this format supports compression. Always returns true.
        /// </summary>
        public override bool SupportsCompression
        {
            get { return true; }
        }


        #region Internal Constructor(BlockSize)
        /// <summary>
        /// Creates a new generic instance of the Huffman compression format.
        /// </summary>
        /// <param name="blockSize">The block size used.</param>
        internal Huffman(BlockSize blockSize)
            : base((byte)blockSize)
        {
            this.CompressBlockSize = blockSize;
        }
        #endregion


        #region Decompression method
        /// <summary>
        /// Decompresses the given stream, writing the decompressed data to the given output stream.
        /// Assumes <code>Supports(instream)</code> returns <code>true</code>.
        /// After this call, the input stream will be positioned at the end of the compressed stream,
        /// or at the initial position + <code>inLength</code>, whichever comes first.
        /// </summary>
        /// <param name="instream">The stream to decompress. At the end of this method, the position
        /// of this stream is directly after the compressed data.</param>
        /// <param name="inLength">The length of the input data. Not necessarily all of the
        /// input data may be read (if there is padding, for example), however never more than
        /// this number of bytes is read from the input stream.</param>
        /// <param name="outstream">The stream to write the decompressed data to.</param>
        /// <returns>The length of the output data.</returns>
        /// <exception cref="NotEnoughDataException">When the given length of the input data
        /// is not enough to properly decompress the input.</exception>
        public override long Decompress(Stream instream, long inLength, Stream outstream)
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
            if (type != (byte)this.CompressBlockSize)
                throw new InvalidDataException("The provided stream is not a valid Huffman "
                            + "compressed stream (invalid type 0x" + type.ToString("X") + "); unknown block size.");
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
                        data = IOUtils.ToNDSu32(buffer, 0);
                        bitsLeft = 32;
                    }
                    // get the next bit
                    bitsLeft--;
                    bool nextIsOne = (data & (1 << bitsLeft)) != 0;
                    // go to the next node, the direction of the child depending on the value of the current/next bit
                    currentNode = nextIsOne ? currentNode.Child1 : currentNode.Child0;
                }
                #endregion

                #region write the data in the current node (when possible)
                switch (this.CompressBlockSize)
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
                        throw new Exception("Unknown block size " + this.CompressBlockSize.ToString());
                }
                #endregion

                outstream.Flush();

                // make sure to start over next round
                currentNode = rootNode;
            }

            // the data is 4-byte aligned. Although very unlikely in this case (compressed bit blocks
            // are always 4 bytes long, and the tree size is generally 4-byte aligned as well),
            // skip any padding due to alignment.
            if (readBytes % 4 != 0)
                readBytes += 4 - (readBytes % 4);

            if (readBytes < inLength)
            {
                throw new TooMuchInputException(readBytes, inLength);
            }

            return decompressedSize;
        }
        #endregion

        #region Utility method: GetLowest(leafQueue, nodeQueue, out prio)
        /// <summary>
        /// Gets the tree node with the lowest priority (frequency) from the leaf and node queues.
        /// If the priority is the same for both head items in the queues, the node from the leaf queue is picked.
        /// </summary>
        protected HuffTreeNode GetLowest(SimpleReversedPrioQueue<int, HuffTreeNode> leafQueue, SimpleReversedPrioQueue<int, HuffTreeNode> nodeQueue, out int prio)
        {
            if (leafQueue.Count == 0)
                return nodeQueue.Dequeue(out prio);
            else if (nodeQueue.Count == 0)
                return leafQueue.Dequeue(out prio);
            else
            {
                int leafPrio, nodePrio;
                leafQueue.Peek(out leafPrio);
                nodeQueue.Peek(out nodePrio);
                // pick a node from the leaf queue when the priorities are equal.
                if (leafPrio <= nodePrio)
                    return leafQueue.Dequeue(out prio);
                else
                    return nodeQueue.Dequeue(out prio);
            }
        }
        #endregion

        #region Utility class: HuffTreeNode
        /// <summary>
        /// A single node in a Huffman tree.
        /// </summary>
        public class HuffTreeNode
        {
            #region Fields & Properties: Data & IsData
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
            #endregion

            #region Field & Properties: Children & Parent
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
            /// The parent node of this node.
            /// </summary>
            public HuffTreeNode Parent { get; private set; }
            /// <summary>
            /// Determines if this is the Child0 of the parent node. Assumes there is a parent.
            /// </summary>
            public bool IsChild0 { get { return this.Parent.child0 == this; } }
            /// <summary>
            /// Determines if this is the Child1 of the parent node. Assumes there is a parent.
            /// </summary>
            public bool IsChild1 { get { return this.Parent.child1 == this; } }
            #endregion

            #region Field & Property: Depth
            private int depth;
            /// <summary>
            /// Get or set the depth of this node. Will not be set automatically, but
            /// will be set recursively (the depth of all child nodes will be updated when this is set).
            /// </summary>
            public int Depth
            {
                get { return this.depth; }
                set
                {
                    this.depth = value;
                    // recursively set the depth of the child nodes.
                    if (!this.isData)
                    {
                        this.child0.Depth = this.depth + 1;
                        this.child1.Depth = this.depth + 1;
                    }
                }
            }
            #endregion

            #region Property: Size
            /// <summary>
            /// Calculates the size of the sub-tree with this node as root.
            /// </summary>
            public int Size
            {
                get
                {
                    if (this.IsData)
                        return 1;
                    return 1 + this.child0.Size + this.child1.Size;
                }
            }
            #endregion

            /// <summary>
            /// The index of this node in the array for building the proper ordering.
            /// If -1, this node has not yet been placed in the array.
            /// </summary>
            internal int index = -1;

            #region Constructor(data, isData, child0, child1)
            /// <summary>
            /// Manually creates a new node for a huffman tree.
            /// </summary>
            /// <param name="data">The data for this node.</param>
            /// <param name="isData">If this node represents data.</param>
            /// <param name="child0">The child of this node on the 0 side.</param>
            /// <param name="child1">The child of this node on the 1 side.</param>
            public HuffTreeNode(byte data, bool isData, HuffTreeNode child0, HuffTreeNode child1)
            {
                this.data = data;
                this.isData = isData;
                this.child0 = child0;
                this.child1 = child1;
                this.isFilled = true;
                if (!isData)
                {
                    this.child0.Parent = this;
                    this.child1.Parent = this;
                }
            }
            #endregion

            #region Constructor(Stream, isData, relOffset, maxStreamPos)
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
                    this.child0.Parent = this;
                    // the 1-node is directly behind the 0-node
                    this.child1 = new HuffTreeNode(stream, oneIsData, zeroRelOffset + 1, maxStreamPos);
                    this.child1.Parent = this;

                    // reset the stream position to right behind this node's data
                    stream.Position = currStreamPos;
                }
            }
            #endregion

            /// <summary>
            /// Generates and returns a string-representation of the huffman tree starting at this node.
            /// </summary>
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
        #endregion
    }

    /// <summary>
    /// The Huffman compression scheme using 4-bit data blocks.
    /// </summary>
    public sealed class Huffman4 : Huffman
    {
        /// <summary>
        /// Gets a short string identifying this compression format.
        /// </summary>
        public override string ShortFormatString
        {
            get { return "Huffman-4"; }
        }

        /// <summary>
        /// Gets a short description of this compression format.
        /// </summary>
        public override string Description
        {
            get { return "Huffman compression scheme using 4-bit datablocks."; }
        }

        /// <summary>
        /// Gets the value that must be given on the command line in order to compress using this format.
        /// </summary>
        public override string CompressionFlag
        {
            get { return "huff4"; }
        }

        /// <summary>
        /// Creates a new instance of the 4-bit Huffman compression format.
        /// </summary>
        public Huffman4()
            : base(BlockSize.FOURBIT) { }

        #region 4-bit block size Compression method
        /// <summary>
        /// Applies Huffman compression with a datablock size of 4 bits.
        /// </summary>
        /// <param name="instream">The stream to compress.</param>
        /// <param name="inLength">The length of the input stream.</param>
        /// <param name="outstream">The stream to write the decompressed data to.</param>
        /// <returns>The size of the decompressed data.</returns>
        public override int Compress(Stream instream, long inLength, Stream outstream)
        {
            if (inLength > 0xFFFFFF)
                throw new InputTooLargeException();

            // cache the input, as we need to build a frequency table
            byte[] inputData = new byte[inLength];
            instream.Read(inputData, 0, (int)inLength);

            // build that frequency table.
            int[] frequencies = new int[0x10];
            for (int i = 0; i < inLength; i++)
            {
                frequencies[inputData[i] & 0xF]++;
                frequencies[(inputData[i] >> 4) & 0xF]++;
            }

            #region Build the Huffman tree

            SimpleReversedPrioQueue<int, HuffTreeNode> leafQueue = new SimpleReversedPrioQueue<int, HuffTreeNode>();
            SimpleReversedPrioQueue<int, HuffTreeNode> nodeQueue = new SimpleReversedPrioQueue<int, HuffTreeNode>();
            int nodeCount = 0;
            // make all leaf nodes, and put them in the leaf queue. Also save them for later use.
            HuffTreeNode[] leaves = new HuffTreeNode[0x10];
            for (int i = 0; i < 0x10; i++)
            {
                // there is no need to store leaves that are not used
                if (frequencies[i] == 0)
                    continue;
                HuffTreeNode node = new HuffTreeNode((byte)i, true, null, null);
                leaves[i] = node;
                leafQueue.Enqueue(frequencies[i], node);
                nodeCount++;
            }

            while (leafQueue.Count + nodeQueue.Count > 1)
            {
                // get the two nodes with the lowest priority.
                HuffTreeNode one = null, two = null;
                int onePrio, twoPrio;
                one = GetLowest(leafQueue, nodeQueue, out onePrio);
                two = GetLowest(leafQueue, nodeQueue, out twoPrio);

                // give those two a common parent, and put that node in the node queue
                HuffTreeNode newNode = new HuffTreeNode(0, false, one, two);
                nodeQueue.Enqueue(onePrio + twoPrio, newNode);
                nodeCount++;
            }
            int rootPrio;
            HuffTreeNode root = nodeQueue.Dequeue(out rootPrio);
            // set the depth of all nodes in the tree, such that we know for each leaf how long
            // its codeword is.
            root.Depth = 0;

            #endregion

            // now that we have a tree, we can write that tree and follow with the data.

            // write the compression header first
            outstream.WriteByte((byte)BlockSize.FOURBIT); // this is block size 4 only
            outstream.WriteByte((byte)(inLength & 0xFF));
            outstream.WriteByte((byte)((inLength >> 8) & 0xFF));
            outstream.WriteByte((byte)((inLength >> 16) & 0xFF));

            int compressedLength = 4;

            #region write the tree

            outstream.WriteByte((byte)((nodeCount - 1) / 2));
            compressedLength++;

            // use a breadth-first traversal to store the tree, such that we do not need to store/calculate the side of each sub-tree.
            // because the data is only 4 bits long, no tree will ever let the offset field overflow.
            LinkedList<HuffTreeNode> printQueue = new LinkedList<HuffTreeNode>();
            printQueue.AddLast(root);
            while (printQueue.Count > 0)
            {
                HuffTreeNode node = printQueue.First.Value;
                printQueue.RemoveFirst();
                if (node.IsData)
                {
                    outstream.WriteByte(node.Data);
                }
                else
                {
                    // bits 0-5: 'offset' = # nodes in queue left
                    // bit 6: node1 end flag
                    // bit 7: node0 end flag
                    byte data = (byte)(printQueue.Count / 2);
                    if (data > 0x3F)
                        throw new InvalidDataException("BUG: offset overflow in 4-bit huffman.");
                    data = (byte)(data & 0x3F);
                    if (node.Child0.IsData)
                        data |= 0x80;
                    if (node.Child1.IsData)
                        data |= 0x40;
                    outstream.WriteByte(data);

                    printQueue.AddLast(node.Child0);
                    printQueue.AddLast(node.Child1);
                }
                compressedLength++;
            }

            #endregion

            #region write the data

            // the codewords are stored in blocks of 32 bits
            uint datablock = 0;
            byte bitsLeftToWrite = 32;

            for (int i = 0; i < inLength; i++)
            {
                byte data = inputData[i];

                for (int j = 0; j < 2; j++)
                {
                    HuffTreeNode node = leaves[(data >> (4 - j * 4)) & 0xF];
                    // the depth of the node is the length of the codeword required to encode the byte
                    int depth = node.Depth;
                    bool[] path = new bool[depth];
                    for (int d = 0; d < depth; d++)
                    {
                        path[depth - d - 1] = node.IsChild1;
                        node = node.Parent;
                    }
                    for (int d = 0; d < depth; d++)
                    {
                        if (bitsLeftToWrite == 0)
                        {
                            outstream.Write(IOUtils.FromNDSu32(datablock), 0, 4);
                            compressedLength += 4;
                            datablock = 0;
                            bitsLeftToWrite = 32;
                        }
                        bitsLeftToWrite--;
                        if (path[d])
                            datablock |= (uint)(1 << bitsLeftToWrite);
                        // no need to OR the buffer with 0 if it is child0
                    }

                }
            }

            // write the partly filled data block as well
            if (bitsLeftToWrite != 32)
            {
                outstream.Write(IOUtils.FromNDSu32(datablock), 0, 4);
                compressedLength += 4;
            }

            #endregion

            return compressedLength;
        }
        #endregion
    }

    /// <summary>
    /// The Huffman compression scheme using 8-bit data blocks.
    /// </summary>
    public sealed class Huffman8 : Huffman
    {
        /// <summary>
        /// Gets a short string identifying this compression format.
        /// </summary>
        public override string ShortFormatString
        {
            get { return "Huffman-8"; }
        }

        /// <summary>
        /// Gets a short description of this compression format.
        /// </summary>
        public override string Description
        {
            get { return "Huffman compression scheme using 8-bit datablocks."; }
        }

        /// <summary>
        /// Gets the value that must be given on the command line in order to compress using this format.
        /// </summary>
        public override string CompressionFlag
        {
            get { return "huff8"; }
        }

        /// <summary>
        /// Creates a new instance of the 4-bit Huffman compression format.
        /// </summary>
        public Huffman8()
            : base(BlockSize.EIGHTBIT) { }

        #region 8-bit block size Compression method
        /// <summary>
        /// Applies Huffman compression with a datablock size of 8 bits.
        /// </summary>
        /// <param name="instream">The stream to compress.</param>
        /// <param name="inLength">The length of the input stream.</param>
        /// <param name="outstream">The stream to write the decompressed data to.</param>
        /// <returns>The size of the decompressed data.</returns>
        public override int Compress(Stream instream, long inLength, Stream outstream)
        {
            if (inLength > 0xFFFFFF)
                throw new InputTooLargeException();

            // cache the input, as we need to build a frequency table
            byte[] inputData = new byte[inLength];
            instream.Read(inputData, 0, (int)inLength);

            // build that frequency table.
            int[] frequencies = new int[0x100];
            for (int i = 0; i < inLength; i++)
                frequencies[inputData[i]]++;

            #region Build the Huffman tree

            SimpleReversedPrioQueue<int, HuffTreeNode> leafQueue = new SimpleReversedPrioQueue<int, HuffTreeNode>();
            SimpleReversedPrioQueue<int, HuffTreeNode> nodeQueue = new SimpleReversedPrioQueue<int, HuffTreeNode>();
            int nodeCount = 0;
            // make all leaf nodes, and put them in the leaf queue. Also save them for later use.
            HuffTreeNode[] leaves = new HuffTreeNode[0x100];
            for (int i = 0; i < 0x100; i++)
            {
                // there is no need to store leaves that are not used
                if (frequencies[i] == 0)
                    continue;
                HuffTreeNode node = new HuffTreeNode((byte)i, true, null, null);
                leaves[i] = node;
                leafQueue.Enqueue(frequencies[i], node);
                nodeCount++;
            }

            while (leafQueue.Count + nodeQueue.Count > 1)
            {
                // get the two nodes with the lowest priority.
                HuffTreeNode one = null, two = null;
                int onePrio, twoPrio;
                one = GetLowest(leafQueue, nodeQueue, out onePrio);
                two = GetLowest(leafQueue, nodeQueue, out twoPrio);

                // give those two a common parent, and put that node in the node queue
                HuffTreeNode newNode = new HuffTreeNode(0, false, one, two);
                nodeQueue.Enqueue(onePrio + twoPrio, newNode);
                nodeCount++;
            }
            int rootPrio;
            HuffTreeNode root = nodeQueue.Dequeue(out rootPrio);
            // set the depth of all nodes in the tree, such that we know for each leaf how long
            // its codeword is.
            root.Depth = 0;

            #endregion

            // now that we have a tree, we can write that tree and follow with the data.

            // write the compression header first
            outstream.WriteByte((byte)BlockSize.EIGHTBIT); // this is block size 8 only
            outstream.WriteByte((byte)(inLength & 0xFF));
            outstream.WriteByte((byte)((inLength >> 8) & 0xFF));
            outstream.WriteByte((byte)((inLength >> 16) & 0xFF));

            int compressedLength = 4;

            #region write the tree

            outstream.WriteByte((byte)((nodeCount - 1) / 2));
            compressedLength++;

            // use a breadth-first traversal to store the tree, such that we do not need to store/calculate the size of each sub-tree.
            // NO! BF results in an ordering that may overflow the offset field.
            
            // find the BF order of all nodes that have two leaves as children. We're going to insert them in an array in reverse BF order,
            // inserting the parent whenever both children have been inserted.
            
            LinkedList<HuffTreeNode> leafStemQueue = new LinkedList<HuffTreeNode>();

            #region fill the leaf queue; first->last will be reverse BF
            LinkedList<HuffTreeNode> nodeCodeStack = new LinkedList<HuffTreeNode>();
            nodeCodeStack.AddLast(root);
            while (nodeCodeStack.Count > 0)
            {
                HuffTreeNode node = nodeCodeStack.First.Value;
                nodeCodeStack.RemoveFirst();
                if (node.IsData)
                    continue;
                if (node.Child0.IsData && node.Child1.IsData)
                {
                    leafStemQueue.AddFirst(node);
                }
                else
                {
                    nodeCodeStack.AddLast(node.Child0);
                    nodeCodeStack.AddLast(node.Child1);
                }

            }
            #endregion

            HuffTreeNode[] nodeArray = new HuffTreeNode[0x1FF]; // this array does not contain the leaves themselves!
            while (leafStemQueue.Count > 0)
            {
                Insert(leafStemQueue.First.Value, nodeArray, 0x3F + 1);
                leafStemQueue.RemoveFirst();
            }

            // update the indices to ignore all gaps
            int nodeIndex = 0;
            for (int i = 0; i < nodeArray.Length; i++)
            {
                if (nodeArray[i] != null)
                    nodeArray[i].index = nodeIndex++;
            }

            // write the nodes in their given order. However when 'writing' a node, write the data of its children instead.
            // the root node is always the first node.
            byte rootData = 0;
            if (root.Child0.IsData)
                rootData |= 0x80;
            if (root.Child1.IsData)
                rootData |= 0x40;
            outstream.WriteByte(rootData); compressedLength++;

            for (int i = 0; i < nodeArray.Length; i++)
            {
                if (nodeArray[i] != null)
                {
                    // nodes in this array are never data!
                    HuffTreeNode node0 = nodeArray[i].Child0;
                    if (node0.IsData)
                        outstream.WriteByte(node0.Data);
                    else
                    {
                        int offset = node0.index - nodeArray[i].index - 1;
                        if (offset > 0x3F)
                            throw new Exception("Offset overflow!");
                        byte data = (byte)offset;
                        if (node0.Child0.IsData)
                            data |= 0x80;
                        if (node0.Child1.IsData)
                            data |= 0x40;
                        outstream.WriteByte(data);
                    }

                    HuffTreeNode node1 = nodeArray[i].Child1;
                    if (node1.IsData)
                        outstream.WriteByte(node1.Data);
                    else
                    {
                        int offset = node1.index - nodeArray[i].index - 1;
                        if (offset > 0x3F)
                            throw new Exception("Offset overflow!");
                        byte data = (byte)offset;
                        if (node0.Child0.IsData)
                            data |= 0x80;
                        if (node0.Child1.IsData)
                            data |= 0x40;
                        outstream.WriteByte(data);
                    }

                    compressedLength += 2;
                }
            }
            #endregion

            #region write the data

            // the codewords are stored in blocks of 32 bits
            uint datablock = 0;
            byte bitsLeftToWrite = 32;

            for (int i = 0; i < inLength; i++)
            {
                byte data = inputData[i];
                HuffTreeNode node = leaves[data];
                // the depth of the node is the length of the codeword required to encode the byte
                int depth = node.Depth;
                bool[] path = new bool[depth];
                for (int d = 0; d < depth; d++)
                {
                    path[depth - d - 1] = node.IsChild1;
                    node = node.Parent;
                }
                for (int d = 0; d < depth; d++)
                {
                    if (bitsLeftToWrite == 0)
                    {
                        outstream.Write(IOUtils.FromNDSu32(datablock), 0, 4);
                        compressedLength += 4;
                        datablock = 0;
                        bitsLeftToWrite = 32;
                    }
                    bitsLeftToWrite--;
                    if (path[d])
                        datablock |= (uint)(1 << bitsLeftToWrite);
                    // no need to OR the buffer with 0 if it is child0
                }
            }

            // write the partly filled data block as well
            if (bitsLeftToWrite != 32)
            {
                outstream.Write(IOUtils.FromNDSu32(datablock), 0, 4);
                compressedLength += 4;
            }

            #endregion

            return compressedLength;
        }
        #endregion

        #region Utility Method: Insert(node, HuffTreeNode[], maxOffset)
        /// <summary>
        /// Inserts the given node into the given array, in such a location that
        /// the offset to both of its children is at most the given maximum, and as large as possible.
        /// In order to do this, the contents of the array may be shifted to the right.
        /// </summary>
        /// <param name="node">The node to insert.</param>
        /// <param name="array">The array to insert the node in.</param>
        /// <param name="maxOffset">The maximum offset between parent and children.</param>
        private void Insert(HuffTreeNode node, HuffTreeNode[] array, int maxOffset)
        {
            // if the node has two data-children, insert it as far to the end as possible.
            if (node.Child0.IsData && node.Child1.IsData)
            {
                for (int i = array.Length - 1; i >= 0; i--)
                {
                    if (array[i] == null)
                    {
                        array[i] = node;
                        node.index = i;
                        break;
                    }
                }
            }
            else
            {
                // if the node is not data, insert it as far left as possible.
                // we know that both children are already present.
                int offset = Math.Max(node.Child0.index - maxOffset, node.Child1.index - maxOffset);
                offset = Math.Max(0, offset);
                if (offset >= node.Child0.index || offset >= node.Child1.index)
                {
                    // it may be that the childen are too far apart, with lots of empty entries in-between.
                    // shift the bottom child right until the node fits in its left-most place for the top child.
                    // (there should be more than enough room in the array)
                    while (offset >= Math.Min(node.Child0.index, node.Child1.index))
                        ShiftRight(array, Math.Min(node.Child0.index, node.Child1.index), maxOffset);
                    while (array[offset] != null)
                        ShiftRight(array, offset, maxOffset);
                    array[offset] = node;
                    node.index = offset;
                }
                else
                {
                    for (int i = offset; i < node.Child0.index && i < node.Child1.index; i++)
                    {
                        if (array[i] == null)
                        {
                            array[i] = node;
                            node.index = i;
                            break;
                        }
                    }
                }
            }

            if (node.index < 0)
                throw new Exception("Node could not be inserted!");

            // if the insertion of this node means that the parent has both children inserted, insert the parent.
            if (node.Parent != null)
            {
                if ((node.Parent.Child0.index >= 0 || node.Parent.Child0.IsData)
                    && (node.Parent.Child1.index >= 0 || node.Parent.Child1.IsData))
                    Insert(node.Parent, array, maxOffset);
            }
        }
        #endregion

        #region Utility Method: ShiftRight(HuffTreeNode[], index, maxOffset)
        /// <summary>
        /// Shifts the node at the given index one to the right.
        /// If the distance between parent and child becomes too large due to this shift, the parent is shifted as well.
        /// </summary>
        /// <param name="array">The array to shift the node in.</param>
        /// <param name="idx">The index of the node to shift.</param>
        /// <param name="maxOffset">The maximum distance between parent and children.</param>
        private void ShiftRight(HuffTreeNode[] array, int idx, int maxOffset)
        {
            HuffTreeNode node = array[idx];
            if (array[idx + 1] != null)
                ShiftRight(array, idx + 1, maxOffset);
            if (node.Parent.index > 0 && node.index - maxOffset + 1 > node.Parent.index)
                ShiftRight(array, node.Parent.index, maxOffset);
            if (node != array[idx])
                return; // already done indirectly.
            array[idx + 1] = array[idx];
            array[idx] = null;
            node.index++;
        }
        #endregion
    }

    /// <summary>
    /// Composite compression format representing both Huffman compression schemes.
    /// </summary>
    public class HuffmanAny : CompositeFormat
    {
        /// <summary>
        /// Creates a new instance of the general Huffman compression format.
        /// </summary>
        public HuffmanAny()
            : base(new Huffman4(), new Huffman8()) { }

        /// <summary>
        /// Gets a short string identifying this compression format.
        /// </summary>
        public override string ShortFormatString
        {
            get { return "Huffman"; }
        }

        /// <summary>
        /// Gets a short description of this compression format.
        /// </summary>
        public override string Description
        {
            get { return "Either the Huffman-4 or Huffman-8 format."; }
        }

        /// <summary>
        /// Gets if this format supports compression. Always returns true.
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
            get { return "huff"; }
        }
    }
}
