using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace JanjaCord.Client.Media;
public sealed class LoopingWaveStream(WaveStream source):WaveStream
{
    public override WaveFormat WaveFormat=>source.WaveFormat;
    public override long Length=>source.Length;
    public override long Position {get=>source.Position;set=>source.Position=value;}
    public override int Read(byte[] buffer,int offset,int count)
    {
        var total=0;var restarted=false;
        while(total<count){var read=source.Read(buffer,offset+total,count-total);if(read==0){if(restarted)break;source.Position=0;restarted=true;continue;}total+=read;restarted=false;}
        return total;
    }
    protected override void Dispose(bool disposing){if(disposing)source.Dispose();base.Dispose(disposing);}
}
public sealed class IncomingRingtone:IDisposable
{
    WasapiOut? output;LoopingWaveStream? loop;MMDevice? device;
    public bool IsPlaying=>output?.PlaybackState==PlaybackState.Playing;
    public static string AssetPath=>Path.Combine(AppContext.BaseDirectory,"Assets","incoming-call.mp3");
    public void Start(string? outputDeviceId,string? file=null)
    {
        Stop();
        try{
            var reader=new AudioFileReader(file??AssetPath){Volume=.65f};loop=new LoopingWaveStream(reader);
            using var devices=new MMDeviceEnumerator();
            try{device=string.IsNullOrEmpty(outputDeviceId)?devices.GetDefaultAudioEndpoint(DataFlow.Render,Role.Console):devices.GetDevice(outputDeviceId);}
            catch{device=devices.GetDefaultAudioEndpoint(DataFlow.Render,Role.Console);}
            output=new WasapiOut(device,AudioClientShareMode.Shared,true,100);output.Init(loop);output.Play();
        }catch{Stop();throw;}
    }
    public void Stop(){var playing=output;output=null;try{playing?.Stop();}finally{playing?.Dispose();loop?.Dispose();loop=null;device?.Dispose();device=null;}}
    public void Dispose()=>Stop();
}
