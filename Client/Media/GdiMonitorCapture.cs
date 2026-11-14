using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
namespace JanjaCord.Client.Media;
// Scale directly into a reusable DIB instead of copying a full-resolution bitmap
// and allocating/resizing two GDI images for every transmitted frame.
internal sealed class GdiMonitorCapture:IDisposable
{
    readonly Rectangle bounds;readonly int width,height;
    IntPtr screen,memory,bitmap,previous,bits;
    public GdiMonitorCapture(Rectangle bounds,int maximumWidth)
    {
        if(bounds.Width<2||bounds.Height<2||bounds.Width>16384||bounds.Height>16384||maximumWidth<2)throw new ArgumentException("Área de captura inválida.");
        this.bounds=bounds;var scale=Math.Min(1d,Math.Min((double)maximumWidth/bounds.Width,maximumWidth*9d/16/bounds.Height));width=Math.Max(2,(int)(bounds.Width*scale)&~1);height=Math.Max(2,(int)(bounds.Height*scale)&~1);
        try{
            screen=GetDC(IntPtr.Zero);memory=CreateCompatibleDC(screen);var info=new BitmapInfo{Size=40,Width=width,Height=-height,Planes=1,BitCount=32};
            bitmap=CreateDIBSection(screen,ref info,0,out bits,IntPtr.Zero,0);
            if(screen==IntPtr.Zero||memory==IntPtr.Zero||bitmap==IntPtr.Zero||bits==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            previous=SelectObject(memory,bitmap);if(previous==IntPtr.Zero||previous==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
            SetStretchBltMode(memory,3);
        }catch{Dispose();throw;}
    }
    public VideoPixels Capture()
    {
        ObjectDisposedException.ThrowIf(memory==IntPtr.Zero,this);
        if(!StretchBlt(memory,0,0,width,height,screen,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x00CC0020))throw new Win32Exception(Marshal.GetLastWin32Error(),"Não foi possível capturar o monitor.");
        GdiFlush();var pixels=new byte[checked(width*height*4)];Marshal.Copy(bits,pixels,0,pixels.Length);return new(width,height,pixels);
    }
    public void Dispose(){if(memory!=IntPtr.Zero&&previous!=IntPtr.Zero&&previous!=new IntPtr(-1))SelectObject(memory,previous);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(memory!=IntPtr.Zero)DeleteDC(memory);if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);screen=memory=bitmap=previous=bits=IntPtr.Zero;}
    [StructLayout(LayoutKind.Sequential)]struct BitmapInfo{public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,ImageSize;public int XPels,YPels;public uint ColorsUsed,ColorsImportant,Color;}
    [DllImport("user32.dll",SetLastError=true)]static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")]static extern int SetStretchBltMode(IntPtr dc,int mode);
    [DllImport("gdi32.dll",SetLastError=true)]static extern bool StretchBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int sx,int sy,int sw,int sh,uint operation);
    [DllImport("gdi32.dll")]static extern bool GdiFlush();
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
}
