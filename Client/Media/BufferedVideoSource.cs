using Microsoft.MixedReality.WebRTC;
namespace JanjaCord.Client.Media;
// Reserves a stable RTP sender from the initial SDP. No frames are generated here:
// only real camera/desktop frames explicitly supplied by an active capture are sent.
public sealed class BufferedVideoSource:IDisposable
{
    VideoPixels? latest;
    int disposed;
    public long Submitted,Provided;
    public ExternalVideoTrackSource Source {get;}=null!;
    public BufferedVideoSource(){Source=ExternalVideoTrackSource.CreateFromArgb32Callback(ProvideFrame);}
    public void Put(VideoPixels frame){if(Volatile.Read(ref disposed)==0){Interlocked.Increment(ref Submitted);Volatile.Write(ref latest,frame);}}
    public void Clear()=>Volatile.Write(ref latest,null);
    unsafe void ProvideFrame(in FrameRequest request)
    {
        var frame=Interlocked.Exchange(ref latest,null);if(frame==null||Volatile.Read(ref disposed)!=0)return;
        fixed(byte* p=frame.Bgra){var video=new Argb32VideoFrame{width=(uint)frame.Width,height=(uint)frame.Height,data=(IntPtr)p,stride=frame.Width*4};request.CompleteRequest(video);Interlocked.Increment(ref Provided);}
    }
    public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;Clear();Source.Dispose();}
}
