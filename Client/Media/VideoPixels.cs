using System.Runtime.InteropServices;
using Microsoft.MixedReality.WebRTC;
namespace JanjaCord.Client.Media;
public sealed record VideoPixels(int Width,int Height,byte[] Bgra);
public static class FrameCopy
{
    public static VideoPixels Copy(Argb32VideoFrame frame)
    {
        var w=checked((int)frame.width);var h=checked((int)frame.height);
        if(w<=0||h<=0||w>4096||h>4096)throw new ArgumentException("Quadro inválido.");
        var bytes=new byte[checked(w*h*4)];
        for(var y=0;y<h;y++)Marshal.Copy(frame.data+y*frame.stride,bytes,y*w*4,w*4);
        return new(w,h,bytes);
    }
}
