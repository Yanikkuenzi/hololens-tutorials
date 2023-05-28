public class Frame<T> 
{
    public long timeStamp { get; set; }
    public T[] data { get; set; }

    public Frame(long timeStamp, T[] data)
    {
        this.timeStamp = timeStamp;
        this.data = data;
    }
}
