using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
namespace JanjaCord.Client.Media;
public sealed record ShareTarget(string Label,IntPtr Window,Rectangle Bounds)
{public override string ToString()=>Label;}
public sealed class DesktopCapture:IDisposable
{
    readonly CancellationTokenSource stop=new();
    Task? captureTask;
    public event Action<VideoPixels>? Preview;
    public event Action<string>? Error;
    public static List<ShareTarget> Targets()
    {
        var result=new List<ShareTarget>();var index=0;
        EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr monitor,IntPtr hdc,ref RECT bounds,IntPtr data)=>{result.Add(new($"Monitor {++index} · {bounds.Right-bounds.Left} × {bounds.Bottom-bounds.Top}",IntPtr.Zero,new(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top)));return true;},IntPtr.Zero);
        EnumWindows((window,_)=>{if(!IsWindowVisible(window)||IsIconic(window))return true;var title=new StringBuilder(256);GetWindowText(window,title,title.Capacity);if(title.Length>0&&GetWindowRect(window,out var bounds)&&bounds.Right-bounds.Left>80&&bounds.Bottom-bounds.Top>60)result.Add(new("Janela · "+title,window,new(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top)));return true;},IntPtr.Zero);
        return result;
    }
    public void Start(ShareTarget target,int width=1280,int fps=15)
    {
        if(fps is <1 or >60)throw new ArgumentOutOfRangeException(nameof(fps),"Use de1 a60 FPS.");
        if(captureTask!=null)throw new InvalidOperationException("Captura já iniciada.");
        captureTask=Task.Run(async()=>{
            try {
                using var monitor=target.Window==IntPtr.Zero?new GdiMonitorCapture(target.Bounds,width):null;
                using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1.0/fps));
                do {
                    var pixels=monitor?.Capture()??Capture(target,width);if(!stop.IsCancellationRequested)Preview?.Invoke(pixels);
                }while(await timer.WaitForNextTickAsync(stop.Token));
            }catch(OperationCanceledException){}catch(Exception ex){Error?.Invoke("Captura interrompida: "+ex.Message);}
        });
    }
    public static VideoPixels Capture(ShareTarget target,int maximumWidth)
    {
        if(target.Window==IntPtr.Zero){using var monitor=new GdiMonitorCapture(target.Bounds,maximumWidth);return monitor.Capture();}
        var bounds=target.Bounds;
        if(target.Window!=IntPtr.Zero){if(!IsWindow(target.Window)||IsIconic(target.Window)||!GetWindowRect(target.Window,out var r))throw new Exception("A janela foi fechada ou minimizada.");bounds=new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);}
        if(bounds.Width<2||bounds.Height<2||bounds.Width>16384||bounds.Height>16384)throw new Exception("Área de captura inválida.");
        using var full=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(full)){
            if(target.Window==IntPtr.Zero)graphics.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size);
            else {var dc=graphics.GetHdc();bool success;try{success=PrintWindow(target.Window,dc,2);}finally{graphics.ReleaseHdc(dc);}if(!success)throw new Exception("Este aplicativo não permite captura de janela. Escolha compartilhar o monitor.");}
        }
        var scale=Math.Min(1.0,Math.Min((double)maximumWidth/bounds.Width,maximumWidth*9.0/16/bounds.Height));var w=Math.Max(2,((int)(bounds.Width*scale))&~1);var h=Math.Max(2,((int)(bounds.Height*scale))&~1);
        using var resized=new Bitmap(w,h,PixelFormat.Format32bppArgb);using(var graphics=Graphics.FromImage(resized)){graphics.DrawImage(full,new Rectangle(0,0,w,h));}
        var data=resized.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {var pixels=new byte[w*h*4];for(var y=0;y<h;y++)Marshal.Copy(data.Scan0+y*data.Stride,pixels,y*w*4,w*4);return new(w,h,pixels);}finally{resized.UnlockBits(data);}
    }
    public void Dispose(){stop.Cancel();/* Capture task owns its bitmap resources and exits on cancellation. */}
    [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
    delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref RECT bounds,IntPtr data);
    delegate bool WindowCallback(IntPtr window,IntPtr data);
    [DllImport("user32.dll")]static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
    [DllImport("user32.dll")]static extern bool EnumWindows(WindowCallback callback,IntPtr data);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder title,int count);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out RECT rect);
    [DllImport("user32.dll")]static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
}
