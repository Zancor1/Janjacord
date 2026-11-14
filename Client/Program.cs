using Avalonia;
namespace JanjaCord.Client;
internal static class Program
{
    [STAThread] public static void Main(string[] args)=>AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
