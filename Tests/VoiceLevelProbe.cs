using JanjaCord.Client.Media;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
public static class VoiceLevelProbe
{
    sealed class Signal(float amplitude,int channels=1,bool opposite=false):ISampleProvider
    {
        int position;public bool Silent;
        public WaveFormat WaveFormat=>WaveFormat.CreateIeeeFloatWaveFormat(48000,channels);
        public int Read(float[] buffer,int offset,int count){for(var i=0;i<count/channels;i++){var value=Silent?0:(float)Math.Sin(position++*2*Math.PI*440/48000)*amplitude;for(var c=0;c<channels;c++)buffer[offset+i*channels+c]=c==0?value:opposite?-value:0;}return count;}
    }
    static double Rms(float[] samples)=>Math.Sqrt(samples.Average(v=>v*v));
    public static void Run()
    {
        foreach(var amplitude in new[]{.001f,.005f,.02f}){
            var signal=new Signal(amplitude);using var processor=new MicrophoneProcessor(false);var block=new float[480];for(var i=0;i<400;i++){signal.Read(block,0,480);processor.Process(block);}
            var rms=Rms(block);var expected=Math.Min(.14,amplitude/Math.Sqrt(2)*128);if(Math.Abs(rms-expected)>expected*.06)throw new Exception("Nivelamento insuficiente para microfone fraco.");
            Console.WriteLine($"PASS: ganho único de captura: RMS entrada {amplitude/Math.Sqrt(2):F5} → saída {rms:F5}");
        }
        foreach(var opposite in new[]{false,true}){var mono=new MicrophoneMonoSource(new Signal(.1f,2,opposite));var block=new float[960];mono.Read(block,0,block.Length);if(Rms(block)<.069)throw new Exception("Microfone estéreo perdeu volume/fase.");}
        var burst=new Signal(.005f);using var normalizer=new MicrophoneProcessor(false);var buffer=new float[480];for(var i=0;i<400;i++){burst.Read(buffer,0,480);normalizer.Process(buffer);}var before=Rms(buffer);burst.Silent=true;for(var i=0;i<100;i++){burst.Read(buffer,0,480);normalizer.Process(buffer);}burst.Silent=false;for(var i=0;i<8;i++){burst.Read(buffer,0,480);normalizer.Process(buffer);}if(Rms(buffer)<before*.94)throw new Exception("Pausa voltou a abaixar a voz.");
        using var encoder=new AudioEncoder(AudioCommonlyUsedFormats.OpusWebRTC);using var decoder=new AudioEncoder(AudioCommonlyUsedFormats.OpusWebRTC);
        var input=new Signal(.001f);using var capture=new MicrophoneProcessor(false);var queue=new BufferedWaveProvider(new WaveFormat(48000,16,1)){BufferDuration=TimeSpan.FromSeconds(1),DiscardOnBufferOverflow=true};var receiver=new VoicePlaybackSource(queue.ToSampleProvider());var rendered=new float[960];var encodedFrame=new float[960];
        for(var i=0;i<200;i++){input.Read(encodedFrame,0,960);capture.Process(encodedFrame.AsSpan(0,480));capture.Process(encodedFrame.AsSpan(480,480));var pcm=encodedFrame.Select(v=>(short)(Math.Clamp(v,-1,1)*32767)).ToArray();var decoded=decoder.DecodeAudio(encoder.EncodeAudio(pcm,AudioCommonlyUsedFormats.OpusWebRTC),AudioCommonlyUsedFormats.OpusWebRTC);var bytes=new byte[decoded.Length*2];Buffer.BlockCopy(decoded,0,bytes,0,bytes.Length);queue.AddSamples(bytes,0,bytes.Length);receiver.Read(rendered,0,rendered.Length);}
        var final=Rms(rendered);if(final<.08||final>.12)throw new Exception("Voz muito baixa após Opus: "+final);
        Console.WriteLine($"PASS: microfone fraco → ganho único → Opus → reprodução sem AGC duplicado: RMS {final:F3}");
    }
}
