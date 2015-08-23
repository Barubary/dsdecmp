A compressor and decompressor for the compression formats commonly used in games made for Nintendo consoles/handhelds.

Supports the following formats:
  * LZ77/LZSS (types 0x10, 0x11 and 'Overlay')
  * Huffman (only data lengths 8 and 4)
  * Run-Length Encoding

'Overlay' LZ compression is used in the 'overlay' files of a game, as well as its arm9 binary. If DSLazy is used to unpack the game, these files are called overlay\_X.bin (with X any number, located in the overlay/ folder) are arm9.bin respectively.

Source is available for both Java and C#. However Overlay LZ do not currently have a Java-implementation, and only the C# implementation can compress files.


If you want to decompress files from Golden Sun: Dark Dawn, use version 3b, found under the deprecated downloads, or version 5 alpha.