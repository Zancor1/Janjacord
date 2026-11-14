using System.Runtime.InteropServices;
using NAudio.Dsp;
namespace JanjaCord.Client.Media;
// Local mono 48 kHz processing in fixed 10 ms blocks, before Opus.
public sealed class MicrophoneProcessor:IDisposable
{
    public const int FrameSize=480;
    readonly float[] nativeInput=new float[FrameSize],nativeOutput=new float[FrameSize];
    // Two frames of pre-roll preserve consonants while rejecting isolated clicks.
    readonly float[] dryDelay=new float[FrameSize*3],wetDelay=new float[FrameSize*2];int dryCursor,wetCursor;
    readonly BiQuadFilter highPass=BiQuadFilter.HighPassFilter(48000,70,.707f);
    readonly LookAheadPeakLimiter safety=new(48000,1);
    readonly bool advanced;DeepFilter? deepFilter;readonly float[] advancedAudio=new float[FrameSize];
    IntPtr state;float inputGain=4,wet,gate,strength;int warmup,voiceHold,voiceFrames;bool enabled;
    public bool Enabled {get=>enabled;set{if(enabled==value)return;if(value&&strength>0&&state==IntPtr.Zero)Create();enabled=value;}}
    public float Strength {get=>strength;set{var next=float.IsFinite(value)?Math.Clamp(value,0,1):1;if(next>0&&enabled&&state==IntPtr.Zero)Create();strength=next;}}
    public float SpeechProbability {get;private set;}
    public MicrophoneProcessor(bool enabled=true,float strength=1,bool advanced=false){this.advanced=advanced;this.enabled=enabled;this.strength=float.IsFinite(strength)?Math.Clamp(strength,0,1):1;if(enabled&&this.strength>0)Create();}
    void Create(){state=rnnoise_create(IntPtr.Zero);if(state==IntPtr.Zero)throw new InvalidOperationException("Não foi possível iniciar o supressor de ruído.");try{if(advanced)deepFilter=new DeepFilter();}catch{rnnoise_destroy(state);state=IntPtr.Zero;throw;}warmup=5;gate=0;voiceHold=voiceFrames=0;Array.Clear(wetDelay);}
    public unsafe void Process(Span<float> samples)
    {
        if(samples.Length!=FrameSize)throw new ArgumentException("Use blocos de 480 amostras.");
        double energy=0;float peak=0;var startingGain=inputGain;var startingGate=gate;
        for(var i=0;i<FrameSize;i++){var sample=float.IsFinite(samples[i])?samples[i]:0;samples[i]=sample;energy+=sample*sample;peak=Math.Max(peak,Math.Abs(sample));}
        var rms=Math.Sqrt(energy/FrameSize);var desired=rms>.0005?Math.Clamp(.14/rms,1,128):inputGain;
        var rate=1-Math.Exp(-.01/(desired<inputGain?.035:.65));inputGain+=(float)((desired-inputGain)*rate);
        if(peak>0){var ceiling=.85f/peak;inputGain=Math.Min(inputGain,ceiling);startingGain=Math.Min(startingGain,ceiling);}
        for(var i=0;i<FrameSize;i++)samples[i]*=startingGain+(inputGain-startingGain)*(i+1)/FrameSize;
        if(state!=IntPtr.Zero){
            for(var i=0;i<FrameSize;i++)nativeInput[i]=highPass.Transform(samples[i])*32768f;
            fixed(float* input=nativeInput)fixed(float* output=nativeOutput)SpeechProbability=rnnoise_process_frame(state,output,input);
            // RNNoise supplies VAD only in advanced mode; DeepFilter supplies the audio.
            if(deepFilter!=null){for(var i=0;i<FrameSize;i++)advancedAudio[i]=nativeInput[i]/32768f;deepFilter.Process(advancedAudio);for(var i=0;i<FrameSize;i++)nativeOutput[i]=advancedAudio[i]*32768f;}
            // Stronger settings require confident speech, like RNNoise voice plugins.
            var threshold=.55f+.30f*strength;
            voiceFrames=SpeechProbability>=threshold?Math.Min(voiceFrames+1,2):0;
            if(voiceFrames>=2)voiceHold=20;else if(voiceHold>0)voiceHold--;
            var targetGate=voiceHold>0?1f:0f;
            gate+=(targetGate-gate)*(targetGate>gate?.8f:.2f);if(warmup>0)warmup--;
        }
        // A linear blend leaked 25% raw keyboard/breath at the default 0.75.
        // Keep zero as bypass; approach fully filtered audio faster.
        var targetWet=enabled&&warmup==0?1-MathF.Pow(1-strength,4):0f;var step=.2f/FrameSize;
        for(var i=0;i<FrameSize;i++){
            wet+=Math.Clamp(targetWet-wet,-step,step);var dry=samples[i];
            var smoothGate=startingGate+(gate-startingGate)*(i+1)/FrameSize;
            var denoised=wetDelay[wetCursor]*smoothGate;wetDelay[wetCursor]=nativeOutput[i]/32768f;wetCursor=(wetCursor+1)%wetDelay.Length;
            samples[i]=safety.Process(dryDelay[dryCursor]*(1-wet)+denoised*wet);dryDelay[dryCursor]=dry;dryCursor=(dryCursor+1)%dryDelay.Length;
        }
        if((!enabled||strength==0)&&wet<=.00001f&&state!=IntPtr.Zero){rnnoise_destroy(state);state=IntPtr.Zero;deepFilter?.Dispose();deepFilter=null;SpeechProbability=0;}
    }
    public void Dispose(){deepFilter?.Dispose();deepFilter=null;if(state!=IntPtr.Zero){rnnoise_destroy(state);state=IntPtr.Zero;}}
    [DllImport("rnnoise",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr rnnoise_create(IntPtr model);
    [DllImport("rnnoise",CallingConvention=CallingConvention.Cdecl)]static extern void rnnoise_destroy(IntPtr state);
    [DllImport("rnnoise",CallingConvention=CallingConvention.Cdecl)]static extern unsafe float rnnoise_process_frame(IntPtr state,float* output,float* input);
}
