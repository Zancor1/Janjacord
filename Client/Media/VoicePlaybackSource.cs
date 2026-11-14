using NAudio.Wave;
namespace JanjaCord.Client.Media;
// Apply only saved volume: don't amplify noise suppression artifacts twice.
public sealed class VoicePlaybackSource(ISampleProvider source):ISampleProvider
{
    float volume=1,current=1;
    public WaveFormat WaveFormat=>source.WaveFormat;
    public int Percent {get=>(int)Math.Round(Volatile.Read(ref volume)*100);set=>Volatile.Write(ref volume,Math.Clamp(value,0,200)/100f);}
    public int Read(float[] buffer,int offset,int count)
    {
        var read=source.Read(buffer,offset,count);var target=Volatile.Read(ref volume);
        if(target==0){Array.Clear(buffer,offset,read);current=0;return read;}
        var smoothing=1f-(float)Math.Exp(-1d/(WaveFormat.SampleRate*.005));
        for(var i=offset;i<offset+read;i++){current+=(target-current)*smoothing;buffer[i]*=current;}return read;
    }
}
// Five milliseconds of look-ahead scale peaks instead of hard-clipping them.
public sealed class LimitedSampleProvider:ISampleProvider
{
    readonly ISampleProvider source;readonly LookAheadPeakLimiter limiter;
    public WaveFormat WaveFormat=>source.WaveFormat;
    public LimitedSampleProvider(ISampleProvider source){this.source=source;limiter=new(source.WaveFormat.SampleRate,source.WaveFormat.Channels);}
    public int Read(float[] buffer,int offset,int count){var read=source.Read(buffer,offset,count);for(var i=offset;i<offset+read;i++)buffer[i]=limiter.Process(buffer[i]);return read;}
}
public sealed class LookAheadPeakLimiter
{
    readonly float[] delay,peaks;readonly long[] indices;
    int cursor,head,tail;long position;float gain=1;readonly float release;
    public LookAheadPeakLimiter(int sampleRate,int channels){delay=new float[Math.Max(1,sampleRate/200*channels)];peaks=new float[delay.Length+2];indices=new long[peaks.Length];release=1f-(float)Math.Exp(-1d/(sampleRate*channels*.08));}
    public float Process(float sample)
    {
        var input=float.IsFinite(sample)?sample:0;var magnitude=Math.Abs(input);
        while(head!=tail&&indices[head]<position-delay.Length)head=(head+1)%peaks.Length;
        while(head!=tail){var last=(tail+peaks.Length-1)%peaks.Length;if(peaks[last]>magnitude)break;tail=last;}
        peaks[tail]=magnitude;indices[tail]=position;tail=(tail+1)%peaks.Length;
        var desired=peaks[head]>.98f?.98f/peaks[head]:1;gain=desired<gain?desired:gain+(desired-gain)*release;
        var output=delay[cursor]*gain;delay[cursor]=input;cursor=(cursor+1)%delay.Length;position++;return output;
    }
}
