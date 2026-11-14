using NAudio.Wave;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
namespace JanjaCord.Client.Media;
// A separate local Opus round-trip. Has no Connection/peer and never writes audio.
public sealed class MicrophonePreview:IDisposable
{
    readonly SelectedAudio engine=new();
    readonly AudioEncoder decoder=new(AudioCommonlyUsedFormats.OpusWebRTC);
    readonly BufferedWaveProvider playback;
    float level;long packets;bool disposed;
    public float Level=>Volatile.Read(ref level);
    public long Packets=>Interlocked.Read(ref packets);
    public event Action<string>? Error;
    public MicrophonePreview(){playback=engine.AddRemote();engine.Error+=message=>Error?.Invoke(message);engine.Encoded+=Receive;}
    public void Start(string? input,string? output,bool suppression,float strength,bool advanced=true){engine.SetNoiseSuppression(suppression,strength,advanced);engine.Configure(input,output,true);}
    public void SetNoiseSuppression(bool enabled,float strength,bool advanced=true)=>engine.SetNoiseSuppression(enabled,strength,advanced);
    void Receive(byte[] packet){var pcm=decoder.DecodeAudio(packet,AudioCommonlyUsedFormats.OpusWebRTC);double sum=0;foreach(var sample in pcm)sum+=(double)sample*sample;Volatile.Write(ref level,(float)Math.Sqrt(sum/Math.Max(1,pcm.Length))/32768f);var bytes=new byte[pcm.Length*2];Buffer.BlockCopy(pcm,0,bytes,0,bytes.Length);playback.AddSamples(bytes,0,bytes.Length);Interlocked.Increment(ref packets);}
    public void Dispose(){if(disposed)return;disposed=true;engine.Dispose();decoder.Dispose();Volatile.Write(ref level,0);}
}
