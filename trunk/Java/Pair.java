public class Pair<T,U> {
	
	private T first;
	private U second;
	
	public Pair(T one, U two){
		this.first = one;
		this.second = two;
	}
	public Pair(){}
	
	public T getFirst(){ return this.first; }
	public void setFirst(T value){ this.first = value; }
	public U getSecond(){ return this.second; }
	public void setSecond(U value){ this.second = value; }
	public boolean allSet(){ return this.first != null && this.second != null; }

}
