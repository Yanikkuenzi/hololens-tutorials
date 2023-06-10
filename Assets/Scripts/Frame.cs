using System.Numerics;

#if ENABLE_WINMD_SUPPORT
using Windows.Graphics.Imaging;
#endif

public class Frame
{
    public long timeStamp { get; set; }
    public Matrix4x4 extrinsics { get; set; }
    public Frame(long timeStamp)
    {
        this.timeStamp = timeStamp;
    }
}

public class DepthFrame : Frame
{
    //public byte[] data { get; set; }
    public ushort[] data { get; set; }
    public float[] uv { get; set; }
    public float[] xy { get; set; }

    public DepthFrame(long ts, ushort[] data)
        : base(ts)
    {
        this.data = data;
    }
}

#if ENABLE_WINMD_SUPPORT
public class ColorFrame : Frame
{

    public SoftwareBitmap bitmap { get; set;}
    public Matrix4x4 intrinsics { get; set; }

    public ColorFrame(long ts, SoftwareBitmap bmp)
        :base(ts) 
    {
        bitmap = bmp;
    }
}
#endif
