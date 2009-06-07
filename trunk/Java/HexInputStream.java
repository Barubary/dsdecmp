import java.io.DataInputStream;
import java.io.EOFException;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.util.Stack;

public class HexInputStream {

	private volatile InputStream dis;
	private volatile long currPos;
	public long getPosition(){ return this.currPos; }
	public void setPosition(long newPos) throws IOException{ this.skip(newPos - currPos); }
	public void goTo(long pos) throws IOException{ this.setPosition(pos); }
	
	private Stack<Long> positionStack;
	
	public HexInputStream(InputStream baseInputStream) {
		this.dis = baseInputStream;
		this.currPos = 0;
		this.positionStack = new Stack<Long>();
	}
	public HexInputStream(String filename) throws FileNotFoundException {
		this.dis = new DataInputStream(new FileInputStream(new File(filename)));
		this.currPos = 0;
		this.positionStack = new Stack<Long>();
	}
	
	public int available() throws IOException{
		return dis.available();
	}
	
	/** Read the next byte from a file. If the EOF has been reached, -1 is returned */
	public int read() throws IOException{
		int b = dis.read();
		if(b != -1)
			currPos++;
		return b;
	}
	
	/** Read an array of bytes from this stream */
	public int[] readBytes(int length) throws IOException{
		int[] data = new int[length];
		for(int i=0; i<length; i++)
			data[i] = readU8();
		return data;
	}
	
	/** Read a byte from this stream */
	public int readU8() throws IOException {
		int b = dis.read();
		currPos++;
		return b;
	}
	
	/** Read a BigEndian s16 from this stream */
	public short readS16() throws IOException {
		short word = 0;
		for(int i = 0; i < 2; i++)
			word = (short)(word | (readU8() << (8 * i)));
		return word;
	}
	
	/** Read a LittleEndian s16 from this stream */
	public short readlS16() throws IOException {
		short word = 0;
		for(int i = 0; i < 2; i++)
			word = (short)((word << 8) | readU8());
		return word;
	}
	
	/** Read a BigEndian u16 from this stream */
	public int readU16() throws IOException {
		int word = 0;
		for(int i = 0; i < 2; i++)
			word = word | (readU8() << (8 * i));
		return word;
	}
	
	/** Read a LittleEndian u16 from this stream */
	public int readlU16() throws IOException {
		int word = 0;
		for(int i = 0; i < 2; i++)
			word = (word << 8) | readU8();
		return word;
	}
	
	/** Read a BigEndian s32 from this stream (a signed int) */
	public int readS32() throws IOException {
		int dword = 0;
		for(int i = 0; i < 4; i++)
			dword = dword | (readU8() << (8 * i));
		return dword;
	}
	/** Read a LittleEndian s32 from this stream (a signed int) */
	public int readlS32() throws IOException {
		int dword = 0;
		for(int i = 0; i < 4; i++)
			dword = (dword << 8) | readU8();
		return dword;
	}
	
	/** Read a BigEndian u32 from this stream (an unsigned int) */
	public long readU32() throws IOException {
		long dword = 0;
		for(int i = 0; i < 4; i++)
			dword = dword | (readU8() << (8 * i));
		
		return dword;
	}
	/** Read a LittleEndian u32 from this stream (an unsigned int) */
	public long readlU32() throws IOException {
		long dword = 0;
		for(int i = 0; i < 4; i++)
			dword = (dword << 8) | readU8();
		return dword;
	}
	
	/** Read a BigEndian s32 from this stream (a signed int) */
	public long readS64() throws IOException {
		long qword = 0;
		for(int i = 0; i < 8; i++)
			qword = qword | (readU8() << (8 * i));
		return qword;
	}
	/** Read a LittleEndian s32 from this stream (a signed int) */
	public long readlS64() throws IOException {
		long qword = 0;
		for(int i = 0; i < 8; i++)
			qword = (qword << 8) | readU8();
		return qword;
	}
	

	
	/** Peek at the next u8 from this stream */
	public short peekU8() throws IOException{
		try{
			short b = (short)readU8();
			skip(-1);
			return b;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next s16 from this stream */
	public short peekS16() throws IOException{
		try{
			short s = readS16();
			skip(-2);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next little-endian s16 from this stream */
	public short peeklS16() throws IOException{
		try{
			short s = readlS16();
			skip(-2);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next u16 from this stream */
	public int peekU16() throws IOException{
		try{
			int s = readU16();
			skip(-2);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next little-endian u16 from this stream */
	public int peeklU16() throws IOException{
		try{
			int s = readlU16();
			skip(-2);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next s32 from this stream */
	public int peekS32() throws IOException{
		try{
			int s = readS32();
			skip(-4);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next little-endian s32 from this stream */
	public int peeklS32() throws IOException{
		try{
			int s = readlS32();
			skip(-4);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next u32 from this stream */
	public long peekU32() throws IOException{
		try{
			long s = readU32();
			skip(-4);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next little-endian u32 from this stream */
	public long peeklU32() throws IOException{
		try{
			long s = readlU32();
			skip(-4);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next s64 from this stream */
	public long peekS64() throws IOException{
		try{
			long s = readS64();
			skip(-8);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	/** peek at the next little-endian s64 from this stream */
	public long peeklS64() throws IOException{
		try{
			long s = readlS64();
			skip(-8);
			return s;
		} catch (EOFException ex){ return -1; }
	}
	
	
	
	/** Read a String of a certain length from this stream */
	public String readString(int length) throws IOException{
		StringBuffer sbuf = new StringBuffer(length);
		for(int i=0; i<length; i++)
			sbuf.append((char)readU8());
		return sbuf.toString();
	}
	
	/**
	 * Reads a \0-terminated string from this stream
	 * @param totlength the total length of the filed in the data. If -1, read until a \0 is read.
	 */
	public String read0TerminatedString(int totlength) throws IOException{
		if(totlength == -1){
			StringBuffer sbuf = new StringBuffer();
			while(peekU8() != 0)
				sbuf.append((char)readU8());
			readU8(); // read the 0 as well
			return sbuf.toString();
		} else {
			StringBuffer sbuf = new StringBuffer();
			boolean read0 = false;
			for(int i=0; i<totlength; i++){
				if(peekU8() == 0)
					read0 = true;
				char c = (char)readU8();
				if(!read0)
					sbuf.append(c);
			}
			return sbuf.toString();
		}
	}
	
	/** Close this stream */
	public void close() throws IOException{
		dis.close();
	}
	
	/** Skip n bytes */
	public void skip(long n) throws IOException{
		dis.skip(n);
		currPos += n;
	}

	/** Reset this stream */
	public void reset() throws IOException{
		this.goTo(0);
	}
	
	
	
	/** Save the current position on the local stack */
	public void savePosition(){
		this.positionStack.push(this.currPos);
	}
	
	/** Pop the last saved position from the local stack and restore that position */
	public void loadPosition() throws IOException{
		if(!this.positionStack.isEmpty()){
			long pos = this.positionStack.peek();
			this.positionStack.pop();
			this.goTo(pos);
		}
	}
	
	

}
