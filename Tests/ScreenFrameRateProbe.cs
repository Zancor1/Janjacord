using System.Diagnostics;
using JanjaCord.Client.Media;
public static class ScreenFrameRateProbe
{
    public static async Task Run(bool require60)
    {
        foreach(var width in new[]{1280,1920})foreach(var fps in new[]{15,30,60})if(!ScreenShareQuality.Presets.Any(p=>p.Width==width&&p.Fps==fps))throw new Exception("Missing share quality");
        using var source=new BufferedVideoSource();
        long received=0;source.Source.Argb32VideoFrameReady+=_=>Interlocked.Increment(ref received);
        var pixels=new VideoPixels(160,90,new byte[160*90*4]);
        foreach(var fps in new[]{15,30,60}){
            source.Clear();await Task.Delay(200);var before=Interlocked.Read(ref source.Provided);var decoded=Interlocked.Read(ref received);var timer=Stopwatch.StartNew();
            using(var pacing=new PeriodicTimer(TimeSpan.FromSeconds(1d/fps))){while(timer.Elapsed.TotalSeconds<3){source.Put(pixels);await pacing.WaitForNextTickAsync();}}
            var delivered=(Interlocked.Read(ref source.Provided)-before)/timer.Elapsed.TotalSeconds;
            Console.WriteLine($"MEASURE: requested={fps} native={delivered:F1}FPS source frames={(Interlocked.Read(ref received)-decoded)}");
            if(require60&&(delivered<fps*.9||delivered>fps*1.2))throw new Exception("Native video source did not sustain requested FPS");
        }
        Console.WriteLine(require60?"PASS: 15/30/60FPS cross native video source":"PASS: native FPS baseline measured");
    }
}
