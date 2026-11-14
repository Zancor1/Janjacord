using System.Diagnostics;
using JanjaCord.Client.Media;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
public static class NoiseSuppressionProbe
{
    static bool advancedMode;static long processingTicks,processedFrames;
    static float[] Process(float[] input,bool enabled,float strength=1){using var processor=new MicrophoneProcessor(enabled,strength,advancedMode);var output=new float[(input.Length+479)/480*480];input.CopyTo(output,0);var timer=Stopwatch.StartNew();for(var i=0;i<output.Length;i+=480)processor.Process(output.AsSpan(i,480));processingTicks+=timer.ElapsedTicks;processedFrames+=output.Length/480;return output;}
    static double Rms(IEnumerable<float> samples){var array=samples.ToArray();return Math.Sqrt(array.Average(x=>x*x));}
    static float[] Noise(string kind,int size)
    {
        var random=new Random(937);var data=new float[size];float smooth=0,previous=0;
        for(var i=0;i<size;i++){
            var white=(float)(random.NextDouble()*2-1);var t=i/48000d;
            if(kind=="teclado"){var pulse=i%8160;data[i]=pulse<1100?(float)(white*.13*Math.Exp(-pulse/220d)+.035*Math.Sin(pulse*.8)*Math.Exp(-pulse/100d)):white*.0001f;}
            else if(kind=="mouse"){var pulse=i%16800;data[i]=pulse<500?(float)(white*.2*Math.Exp(-pulse/40d)):0;}
            else{smooth+=.12f*(white-smooth);var high=smooth-previous;previous=smooth;data[i]=(float)((.8*smooth+.2*high)*.12*Math.Pow(Math.Max(0,Math.Sin(2*Math.PI*.8*t)),2));}
        }return data;
    }
    static double Correlation(float[] reference,float[] output)
    {
        double best=0;
        for(var lag=0;lag<=960;lag+=24){double dot=0,a=0,b=0;for(var i=4800;i<Math.Min(reference.Length,output.Length-lag);i++){var x=reference[i];var y=output[i+lag];dot+=x*y;a+=x*x;b+=y*y;}best=Math.Max(best,dot/Math.Sqrt(a*b+1e-30));}return best;
    }
    sealed class Sine:ISampleProvider{int index;public WaveFormat WaveFormat=>WaveFormat.CreateIeeeFloatWaveFormat(48000,1);public int Read(float[] buffer,int offset,int count){for(var i=0;i<count;i++)buffer[offset+i]=(float)(.9*Math.Sin(2*Math.PI*1000*index++/48000));return count;}}
    public static void Run(string speechPath,bool advanced=false)
    {
        advancedMode=advanced;processingTicks=processedFrames=0;var clock=Stopwatch.StartNew();
        foreach(var kind in new[]{"teclado","mouse","respiração"}){
            var noise=Noise(kind,48000*5);var bypass=Process(noise,false);var filtered=Process(noise,true);var reduction=20*Math.Log10((Rms(bypass.Skip(48000))+1e-12)/(Rms(filtered.Skip(48000))+1e-12));
            Console.WriteLine($"MEASURE: ruído sintético {kind}: redução {reduction:F1} dB");if(reduction<8)throw new Exception("Supressão insuficiente para "+kind);
        }
        var strengthInput=Noise("teclado",48000*4);var zero=Process(strengthInput,true,0);var without=Process(strengthInput,false);if(zero.Zip(without,(a,b)=>Math.Abs(a-b)).Max()>.000001)throw new Exception("Intensidade0 não equivale a sem supressão.");
        var lastLevel=double.MaxValue;foreach(var strength in new[]{0f,.25f,.5f,.75f,1f}){var level=Rms(Process(strengthInput,true,strength).Skip(48000));Console.WriteLine($"MEASURE: intensidade {strength:F2}: RMS de ruído {level:F5}");if(level>lastLevel*1.01)throw new Exception("Aumentar intensidade não reduziu o ruído.");lastLevel=level;}
        var defaultReduction=20*Math.Log10(Rms(without.Skip(48000))/Math.Max(1e-12,Rms(Process(strengthInput,true,.75f).Skip(48000))));
        if(defaultReduction<40)throw new Exception("O ajuste0.75 voltou a deixar passar teclado sem filtro.");
        using var wave=new WaveFileReader(speechPath);var source=wave.ToSampleProvider();if(source.WaveFormat.SampleRate!=48000||source.WaveFormat.Channels!=1)throw new Exception("Fixture deve ser mono48k.");
        var speech=new List<float>();var temp=new float[4096];int read;while((read=source.Read(temp,0,temp.Length))>0)speech.AddRange(temp.Take(read));var clean=speech.ToArray();var cleanOutput=Process(clean,true);var cleanBypass=Process(clean,false);
        var correlation=Correlation(cleanBypass,cleanOutput);var retention=Rms(cleanOutput)/Rms(cleanBypass);Console.WriteLine($"MEASURE: fala PT-BR sintetizada: correlação {correlation:F3}, energia relativa {retention:F2}");if(correlation<.7||retention<.35)throw new Exception("O supressor comprometeu excessivamente a fala.");
        var quiet=clean.Select(x=>x*.02f).ToArray();var quietOutput=Process(quiet,true);var quietBypass=Process(quiet,false);var quietCorrelation=Correlation(quietBypass,quietOutput);Console.WriteLine($"MEASURE: fala PT-BR 34dB mais baixa: correlação {quietCorrelation:F3}, RMS {Rms(quietOutput):F3}");if(quietCorrelation<.65||Rms(quietOutput)<.025)throw new Exception("O supressor abafou a fala baixa.");
        var background=Noise("teclado",clean.Length);var scale=(float)(Rms(clean)/(Rms(background)*Math.Sqrt(10)));var mixed=clean.Select((sample,i)=>sample+background[i]*scale).ToArray();var mixedOut=Process(mixed,true);var mixedCorrelation=Correlation(cleanBypass,mixedOut);Console.WriteLine($"MEASURE: fala com teclado a SNR10dB: correlação {mixedCorrelation:F3}");if(mixedCorrelation<.6)throw new Exception("Fala com teclado ficou ilegível no teste.");
        using(var toggle=new MicrophoneProcessor(advanced:advancedMode)){var block=new float[480];for(var i=0;i<150;i++){Array.Copy(clean,i*480%Math.Max(480,clean.Length-480),block,0,480);if(i%30==0)toggle.Enabled=!toggle.Enabled;toggle.Process(block);if(block.Any(x=>!float.IsFinite(x)||Math.Abs(x)>1))throw new Exception("Alternância gerou amostras inválidas.");}}
        using(var switched=new MicrophoneProcessor(advanced:advancedMode))using(var raw=new MicrophoneProcessor(false)){var a=new float[480];var b=new float[480];for(var frame=0;frame<100;frame++){Array.Copy(clean,frame*480%Math.Max(480,clean.Length-480),a,0,480);a.CopyTo(b,0);if(frame==40)switched.Enabled=false;switched.Process(a);raw.Process(b);if(frame>=55&&a.Zip(b,(x,y)=>Math.Abs(x-y)).Max()>.00001)throw new Exception("Desligar não restaurou o áudio sem supressão.");}}
        var personal=new VoicePlaybackSource(new Sine()){Percent=200};var limiter=new LimitedSampleProvider(personal);var output=new float[48000];limiter.Read(output,0,output.Length);limiter.Read(output,0,output.Length);
        double sine=0,cosine=0,power=0;for(var i=0;i<output.Length;i++){var angle=2*Math.PI*1000*i/48000;sine+=output[i]*Math.Sin(angle);cosine+=output[i]*Math.Cos(angle);power+=output[i]*output[i];}
        var fundamental=2*(sine*sine+cosine*cosine)/output.Length;var distortion=Math.Sqrt(Math.Max(0,power-fundamental)/fundamental);Console.WriteLine($"MEASURE: sinal alto a200%: distorção residual {distortion*100:F3}%, pico {output.Max(Math.Abs):F3}");if(distortion>.005||output.Any(x=>Math.Abs(x)>.981))throw new Exception("Limitador distorceu/estourou a voz.");
        personal.Percent=0;limiter.Read(output,0,output.Length);limiter.Read(output,0,output.Length);if(output.Any(x=>x!=0))throw new Exception("Volume0 não silenciou.");
        var milliseconds=processingTicks*1000d/Stopwatch.Frequency/processedFrames;Console.WriteLine($"MEASURE: processamento m\u00e9dio {milliseconds:F2}ms por quadro de10ms ({(advancedMode?"DeepFilterNet":"RNNoise")})");if(milliseconds>=10)throw new Exception("Filtro n?o acompanhou tempo real.");
        Console.WriteLine($"PASS: filtro neural real, fala preservada, alternância ao vivo e limitador sem corte brusco ({clock.Elapsed.TotalSeconds:F2}s)");
    }
}
