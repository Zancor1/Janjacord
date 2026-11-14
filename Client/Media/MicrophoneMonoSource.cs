using NAudio.Wave;
namespace JanjaCord.Client.Media;
// Some stereo microphones expose speech on one channel, or opposite phases.
// Averaging those channels halves the voice or cancels it entirely.
public sealed class MicrophoneMonoSource(ISampleProvider source):ISampleProvider
{
    readonly int channels=source.WaveFormat.Channels;
    float[] interleaved=[];
    public WaveFormat WaveFormat=>WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate,1);
    public int Read(float[] buffer,int offset,int count)
    {
        if(interleaved.Length<count*channels)interleaved=new float[count*channels];
        var read=source.Read(interleaved,0,count*channels);var frames=read/channels;int selected=0;double largest=-1;
        for(var channel=0;channel<channels;channel++){double energy=0;for(var i=0;i<frames;i++){var sample=interleaved[i*channels+channel];energy+=sample*sample;}if(energy>largest){largest=energy;selected=channel;}}
        for(var i=0;i<frames;i++)buffer[offset+i]=interleaved[i*channels+selected];return frames;
    }
}
