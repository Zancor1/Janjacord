using JanjaCord.Client.Media;
using NAudio.Wave;
public static class RingtoneProbe
{
    public static async Task Run(string path)
    {
        var format=new WaveFormat(8000,16,1);var sequence=new byte[]{1,2,3,4,5,6,7,8};
        using(var loop=new LoopingWaveStream(new RawSourceWaveStream(new MemoryStream(sequence),format))){var repeated=new byte[sequence.Length*4];if(loop.Read(repeated,0,repeated.Length)!=repeated.Length||repeated.Where((value,i)=>value!=sequence[i%sequence.Length]).Any())throw new Exception("O toque não repetiu ao chegar ao fim.");}
        using(var empty=new LoopingWaveStream(new RawSourceWaveStream(new MemoryStream(),format))){if(empty.Read(new byte[16],0,16)!=0)throw new Exception("Fonte vazia não terminou.");}
        using(var decoder=new AudioFileReader(path)){if(decoder.Length==0)throw new Exception("MP3 vazio.");using var loop=new LoopingWaveStream(decoder);decoder.Position=decoder.Length;var bytes=new byte[65536];var audible=false;for(long read=0;read<decoder.Length+bytes.Length;read+=bytes.Length){if(loop.Read(bytes,0,bytes.Length)!=bytes.Length)throw new Exception("MP3 loop incompleto.");audible|=bytes.Any(b=>b!=0);}if(!audible)throw new Exception("O MP3 não reiniciou ao chegar ao final.");}
        Console.WriteLine("PASS: MP3 fornecido decodifica e o toque repete sem terminar ou travar com fonte vazia");
        using var ringtone=new IncomingRingtone();ringtone.Start(null,path);await Task.Delay(350);if(!ringtone.IsPlaying)throw new Exception("O toque não iniciou no dispositivo de saída.");ringtone.Stop();if(ringtone.IsPlaying)throw new Exception("O toque não parou.");ringtone.Stop();
        Console.WriteLine("PASS: reprodução WASAPI do toque fornecido inicia e para; parar novamente é seguro");
    }
}
