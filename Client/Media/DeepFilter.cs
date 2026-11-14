using System.Runtime.InteropServices;
namespace JanjaCord.Client.Media;
// DeepFilterNet3 low-latency, 48 kHz mono; private state per capture, no files/network.
public sealed class DeepFilter:IDisposable
{
    IntPtr state;
    readonly float[] output=new float[MicrophoneProcessor.FrameSize];
    public DeepFilter()
    {
        state=janja_df_create();
        if(state==IntPtr.Zero)throw new InvalidOperationException("Não foi possível iniciar DeepFilterNet.");
        if(df_get_frame_length(state)!=(nuint)MicrophoneProcessor.FrameSize){Dispose();throw new InvalidOperationException("Modelo DeepFilterNet incompatível.");}
    }
    public unsafe void Process(Span<float> samples)
    {
        ObjectDisposedException.ThrowIf(state==IntPtr.Zero,this);
        if(samples.Length!=MicrophoneProcessor.FrameSize)throw new ArgumentException("Use blocos de480 amostras.");
        float snr;
        fixed(float* input=samples)fixed(float* destination=output)snr=df_process_frame(state,input,destination);
        if(!float.IsFinite(snr)||output.Any(x=>!float.IsFinite(x)))throw new InvalidOperationException("DeepFilterNet retornou áudio inválido. Selecione o modo Leve.");
        output.CopyTo(samples);
    }
    public void Dispose(){if(state==IntPtr.Zero)return;df_free(state);state=IntPtr.Zero;}
    [DllImport("deep_filter",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr janja_df_create();
    [DllImport("deep_filter",CallingConvention=CallingConvention.Cdecl)]static extern nuint df_get_frame_length(IntPtr state);
    [DllImport("deep_filter",CallingConvention=CallingConvention.Cdecl)]static extern unsafe float df_process_frame(IntPtr state,float* input,float* output);
    [DllImport("deep_filter",CallingConvention=CallingConvention.Cdecl)]static extern void df_free(IntPtr state);
}
