using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
namespace JanjaCord.Client.Media;
public sealed record AudioDeviceChoice(string? Id,string Name){public override string ToString()=>Name;}
// WASAPI devices belong to this call only. Never changes the Windows default device.
public sealed class SelectedAudio:IDisposable
{
    readonly object gate=new();
    readonly MixingSampleProvider mixer=new(WaveFormat.CreateIeeeFloatWaveFormat(48000,1)){ReadFully=true};
    readonly Dictionary<BufferedWaveProvider,VoicePlaybackSource> remoteInputs=[];
    readonly AudioEncoder encoder=new(AudioCommonlyUsedFormats.OpusWebRTC);
    readonly short[] frame=new short[960];int filled;
    readonly float[] processing=new float[480];int processingFilled;MicrophoneProcessor? processor;
    public bool NoiseSuppressionEnabled {get;private set;}=true;
    public float NoiseSuppressionStrength {get;private set;}=1;
    public bool NoiseSuppressionAdvanced {get;private set;}=true;
    public void SetNoiseSuppression(bool enabled,float strength=1,bool advanced=true){lock(gate){strength=float.IsFinite(strength)?Math.Clamp(strength,0,1):1;if(processor!=null&&NoiseSuppressionAdvanced!=advanced){var next=new MicrophoneProcessor(enabled,strength,advanced);processor.Dispose();processor=next;filled=processingFilled=0;}if(processor!=null){processor.Strength=strength;processor.Enabled=enabled;}NoiseSuppressionEnabled=enabled;NoiseSuppressionStrength=strength;NoiseSuppressionAdvanced=advanced;}}
    bool transmitPaused,receiveMuted;
    public void PauseTransmission(bool paused){lock(gate){if(transmitPaused==paused)return;transmitPaused=paused;volume.Volume=paused||receiveMuted?0:1;filled=processingFilled=0;if(!paused&&processor!=null){processor.Dispose();processor=new MicrophoneProcessor(NoiseSuppressionEnabled,NoiseSuppressionStrength,NoiseSuppressionAdvanced);}}}
    WasapiCapture? capture;WasapiOut? output;MMDevice? inputDevice,outputDevice;
    VolumeSampleProvider volume;bool closed,muted;
    long encodedPackets;
    public long EncodedPackets=>Interlocked.Read(ref encodedPackets);
    public event Action<byte[]>? Encoded;
    public event Action<string>? Error;
    public SelectedAudio(){volume=new VolumeSampleProvider(new LimitedSampleProvider(mixer));}
    public static AudioDeviceChoice[] Devices(bool input){using var e=new MMDeviceEnumerator();var devices=e.EnumerateAudioEndPoints(input?DataFlow.Capture:DataFlow.Render,DeviceState.Active);return new[]{new AudioDeviceChoice(null,"Padrão do Windows")}.Concat(devices.Select(d=>{using(d)return new AudioDeviceChoice(d.ID,d.FriendlyName);})).ToArray();}
    // Console streams do not ask Windows to attenuate other applications.
    static MMDevice Device(string? id,DataFlow flow){using var e=new MMDeviceEnumerator();return string.IsNullOrEmpty(id)?e.GetDefaultAudioEndpoint(flow,Role.Console):e.GetDevice(id);}
    public void Configure(string? input,string? sink,bool useMicrophone)
    {
        StopCapture();StopOutput();
        lock(gate){if(closed)return;outputDevice=Device(sink,DataFlow.Render);output=new WasapiOut(outputDevice,AudioClientShareMode.Shared,true,60);output.Init(new MonoToStereoSampleProvider(volume).ToWaveProvider());output.Play();}
        if(useMicrophone)StartCapture(input);
    }
    public void StartCapture(string? id)
    {
        StopCapture();lock(gate){if(closed)return;inputDevice=Device(id,DataFlow.Capture);capture=new WasapiCapture(inputDevice,false,20);var buffer=new BufferedWaveProvider(capture.WaveFormat){ReadFully=false,DiscardOnBufferOverflow=true};ISampleProvider samples=buffer.ToSampleProvider();
        if(samples.WaveFormat.Channels!=1)samples=new MicrophoneMonoSource(samples);
        if(samples.WaveFormat.SampleRate!=48000)samples=new WdlResamplingSampleProvider(samples,48000);
        processor?.Dispose();processor=new MicrophoneProcessor(NoiseSuppressionEnabled,NoiseSuppressionStrength,NoiseSuppressionAdvanced);processingFilled=0;
        var source=samples;var temp=new float[4096];filled=0;
        capture.DataAvailable+=(_,a)=>{try{lock(gate){if(closed||muted||transmitPaused)return;buffer.AddSamples(a.Buffer,0,a.BytesRecorded);int read;while((read=source.Read(temp,0,temp.Length))>0)for(var i=0;i<read;i++){processing[processingFilled++]=temp[i];if(processingFilled!=480)continue;processor!.Process(processing);processingFilled=0;foreach(var sample in processing){frame[filled++]=(short)(Math.Clamp(sample,-1f,1f)*32767);if(filled==960){Interlocked.Increment(ref encodedPackets);Encoded?.Invoke(encoder.EncodeAudio(frame,AudioCommonlyUsedFormats.OpusWebRTC));filled=0;}}}}}catch(Exception ex){Error?.Invoke("Falha no microfone: "+ex.Message);}};
        capture.RecordingStopped+=(_,a)=>{if(a.Exception!=null)Error?.Invoke("Microfone desconectado: "+a.Exception.Message);};capture.StartRecording();}
    }
    public void Mute(bool value){lock(gate){muted=value;filled=0;processingFilled=0;}}
    public void Deafen(bool value){lock(gate){receiveMuted=value;volume.Volume=value||transmitPaused?0:1;}}
    public BufferedWaveProvider AddRemote(){lock(gate){var b=new BufferedWaveProvider(new WaveFormat(48000,16,1)){BufferDuration=TimeSpan.FromMilliseconds(300),DiscardOnBufferOverflow=true};var samples=new VoicePlaybackSource(b.ToSampleProvider());remoteInputs[b]=samples;mixer.AddMixerInput(samples);return b;}}
    public void SetRemoteVolume(BufferedWaveProvider buffer,int percent){lock(gate)if(remoteInputs.TryGetValue(buffer,out var voice))voice.Percent=percent;}
    // Removing all inputs at call disposal also releases the per-peer buffers.
    public void RemoveRemote(BufferedWaveProvider buffer){lock(gate){if(remoteInputs.Remove(buffer,out var samples))mixer.RemoveMixerInput(samples);buffer.ClearBuffer();}}
    void StopCapture(){WasapiCapture? old;lock(gate){old=capture;capture=null;}if(old!=null){old.StopRecording();old.Dispose();}inputDevice?.Dispose();inputDevice=null;lock(gate){processor?.Dispose();processor=null;processingFilled=0;}}
    void StopOutput(){WasapiOut? old;lock(gate){old=output;output=null;}if(old!=null){old.Stop();old.Dispose();}outputDevice?.Dispose();outputDevice=null;}
    public void Dispose(){lock(gate)closed=true;StopCapture();StopOutput();lock(gate){mixer.RemoveAllMixerInputs();remoteInputs.Clear();encoder.Dispose();}}
}
