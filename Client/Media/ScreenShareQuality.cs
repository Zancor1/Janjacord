namespace JanjaCord.Client.Media;
public sealed record ScreenShareQuality(int Width,int Fps)
{
    public static IReadOnlyList<ScreenShareQuality> Presets {get;}=new[]{new ScreenShareQuality(1280,15),new(1280,30),new(1280,60),new(1920,15),new(1920,30),new(1920,60)};
    public override string ToString()=>$"{(Width==1920?1080:720)}p · {Fps} FPS";
}
