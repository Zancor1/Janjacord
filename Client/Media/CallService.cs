using System.Text.Json;
using JanjaCord.Client.Services;
using JanjaCord.Shared;
using Microsoft.MixedReality.WebRTC;
namespace JanjaCord.Client.Media;
public sealed class CallService:IAsyncDisposable
{
    readonly Connection connection;
    readonly SemaphoreSlim serial=new(1,1);
    readonly System.Threading.Channels.Channel<Func<Task>> work=System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>(new(){SingleReader=true});
    readonly Task worker;
    readonly System.Collections.Concurrent.ConcurrentDictionary<string,Peer> peers=new();
    readonly Dictionary<string,List<RtcSignal>> earlySignals=[];
    readonly JsonSerializerOptions json=new(){IncludeFields=true};
    SelectedAudio? audio;
    public Func<long,int>? VolumeForUser {get;set;}
    public bool NoiseSuppressionAdvanced {get;set;}=true;
    public bool NoiseSuppressionEnabled {get;set;}=true;
    public float NoiseSuppressionStrength {get;set;}=.9f;
    public async Task SetNoiseSuppression(bool enabled,float? strength=null,bool? advanced=null){await serial.WaitAsync();try{var level=strength??NoiseSuppressionStrength;level=float.IsFinite(level)?Math.Clamp(level,0,1):1;var mode=advanced??NoiseSuppressionAdvanced;audio?.SetNoiseSuppression(enabled,level,mode);NoiseSuppressionAdvanced=mode;NoiseSuppressionEnabled=enabled;NoiseSuppressionStrength=level;Changed?.Invoke();}finally{serial.Release();}}
    bool microphoneTestPaused;
    public async Task PauseMicrophoneForTest(bool paused){await serial.WaitAsync();try{microphoneTestPaused=paused;audio?.PauseTransmission(paused);}finally{serial.Release();}}
    public string? InputDeviceId {get;set;}
    public string? OutputDeviceId {get;set;}
    public long? RoomId=>channel;
    public long SentAudioPackets=>audio?.EncodedPackets??0;
    DeviceVideoTrackSource? camera;
    LocalVideoTrack? cameraPreview;
    DesktopCapture? desktop;
    int shareWidth=1280,shareFps=15;
    void ConfigureVideoRate(PeerConnection rtc){if(desktop==null){rtc.SetBitrate(100000,800000,2500000);return;}uint maximum=(uint)(shareWidth>=1920?(shareFps==60?16000000:8000000):(shareFps==60?10000000:5000000));rtc.SetBitrate(100000,maximum/2,maximum);}
    BufferedVideoSource? cameraFrames,screenFrames;
    long? channel;
    bool disposed;
    public bool Muted {get;private set;}
    public bool Deafened {get;private set;}
    public bool CameraOn=>camera!=null;
    public bool Sharing=>desktop!=null;
    public bool AudioConnected=>peers.Values.Any(p=>p.Voice?.Connected==true);
    public bool AudioFailed=>peers.Values.Any(p=>p.Voice?.Failed==true);
    public bool HasOtherParticipants=>!peers.IsEmpty;
    public bool InCall=>channel!=null;
    public event Action<CallRoster>? Participants;
    public event Action<string,string,VideoPixels>? Frame;
    public event Action<string,string>? State;
    public event Action<string>? Error;
    public event Action? Changed;
    public event Action<string>? Diagnostics;
    public async Task<string> DebugStats()
    {
        await serial.WaitAsync();try{
            var result=$"Source submitted={screenFrames?.Submitted} provided={screenFrames?.Provided}";
            foreach(var p in peers.Values){using var stats=await p.Rtc.GetSimpleStatsAsync();result+="\n"+JsonSerializer.Serialize(stats.GetStats<PeerConnection.VideoSenderStats>().ToArray(),json)+"\n"+JsonSerializer.Serialize(stats.GetStats<PeerConnection.VideoReceiverStats>().ToArray(),json);}
            return result;
        }finally{serial.Release();}
    }
    sealed class Peer:IDisposable
    {
        public required string Id;
        public long UserId;
        public required PeerConnection Rtc;
        public Transceiver Camera=null!,Screen=null!;
        public AudioPeer? Voice;
        public LocalVideoTrack? CameraTrack,ScreenTrack;
        public readonly List<IceCandidate> Ice=[];
        public bool RemoteSet,WaitingAnswer,Dirty;
        public void Dispose(){Rtc.Close();Voice?.Dispose();CameraTrack?.Dispose();ScreenTrack?.Dispose();Rtc.Dispose();}
    }
    public CallService(Connection connection)
    {
        this.connection=connection;connection.Roster+=OnRoster;connection.SignalReceived+=OnSignal;connection.Interrupted+=OnInterrupted;connection.CallEnded+=OnCallEnded;
        worker=Task.Run(async()=>{await foreach(var action in work.Reader.ReadAllAsync()){await serial.WaitAsync();try{if(!disposed)await action();}catch(Exception ex){Error?.Invoke(ex.Message);}finally{serial.Release();}}});
    }
    void Run(Func<Task> action)=>work.Writer.TryWrite(action);
    void OnRoster(CallRoster roster)=>Run(()=>UpdateRoster(roster));
    void OnSignal(RtcSignal signal)=>Run(()=>Receive(signal));
    void OnInterrupted()=>Run(async()=>{await StopLocal();Changed?.Invoke();Error?.Invoke("A conexão caiu. Câmera, tela e microfone foram desligados; entre novamente no canal.");});
    void OnCallEnded(long id)=>Run(async()=>{if(channel==id){await StopLocal();Changed?.Invoke();if(id>=0)Error?.Invoke("A chamada foi encerrada porque o servidor foi excluído.");}});
    public async Task Join(long id,bool enableMicrophone=true)
    {
        await serial.WaitAsync();
        try {
            await StopLocal();await connection.LeaveCall();
            cameraFrames=new BufferedVideoSource();screenFrames=new BufferedVideoSource();
            audio=new SelectedAudio();audio.SetNoiseSuppression(NoiseSuppressionEnabled,NoiseSuppressionStrength,NoiseSuppressionAdvanced);audio.PauseTransmission(microphoneTestPaused);audio.Error+=message=>Error?.Invoke(message);Muted=!enableMicrophone;
            if(enableMicrophone){try{audio.Configure(InputDeviceId,OutputDeviceId,true);audio.Deafen(Deafened);}catch(Exception ex){Muted=true;audio.Mute(true);Error?.Invoke("Não foi possível abrir o áudio selecionado: "+ex.Message);}}
            channel=id;
            try{var roster=await connection.JoinCall(id);await UpdateRoster(roster);}catch{await StopLocal();throw;}
            Changed?.Invoke();
        }finally{serial.Release();}
    }
    async Task UpdateRoster(CallRoster roster)
    {
        if(channel!=roster.ChannelId)return;
        var others=roster.Peers.Where(p=>p.ConnectionId!=connection.ConnectionId).ToArray();
        foreach(var stale in peers.Keys.Except(others.Select(p=>p.ConnectionId)).ToArray()){peers[stale].Dispose();peers.TryRemove(stale,out _);}
        foreach(var other in others)if(!peers.ContainsKey(other.ConnectionId))await CreatePeer(other.ConnectionId,other.UserId);
        foreach(var id in earlySignals.Keys.ToArray())if(peers.ContainsKey(id)){var queued=earlySignals[id];earlySignals.Remove(id);foreach(var item in queued)await Receive(item);}
        Participants?.Invoke(roster);Changed?.Invoke();
    }
    async Task<Peer> CreatePeer(string id,long userId)
    {
        if(peers.TryGetValue(id,out var existing))return existing;
        var rtc=new PeerConnection{PreferredAudioCodec="opus",PreferredVideoCodec="VP8"};
        var p=new Peer{Id=id,Rtc=rtc,UserId=userId};
        rtc.LocalSdpReadytoSend+=sdp=>{Diagnostics?.Invoke(sdp.Type+"\n"+string.Join("\n",sdp.Content.Split('\n').Where(l=>l.StartsWith("m=")||l.StartsWith("a=mid:")||l.StartsWith("a=msid:")||l.StartsWith("a=send")||l.StartsWith("a=recv"))));Run(()=>SendToPeer(id,sdp.Type==SdpMessageType.Offer?"offer":"answer",sdp.Content));};
        rtc.IceCandidateReadytoSend+=ice=>Run(()=>SendToPeer(id,"ice",JsonSerializer.Serialize(ice,json)));
        rtc.IceStateChanged+=state=>{State?.Invoke(id,state.ToString());if(state==IceConnectionState.Failed)Error?.Invoke("Não foi possível estabelecer mídia direta. Verifique o firewall de Client.exe e a rede Radmin.");};
        rtc.VideoTrackAdded+=track=>{
            var kind=track.Name.StartsWith("screen",StringComparison.OrdinalIgnoreCase)||track.Transceiver.MlineIndex==1?"screen":"camera";
            var announced=false;
            var last=0L;
            track.Argb32VideoFrameReady+=frame=>{try{var now=Environment.TickCount64;if(now-Interlocked.Read(ref last)<(kind=="screen"?10:25))return;Interlocked.Exchange(ref last,now);if(!announced){announced=true;State?.Invoke(id,kind+"-on");}Frame?.Invoke(id,kind,FrameCopy.Copy(frame));}catch(Exception ex){Error?.Invoke("Falha ao mostrar vídeo: "+ex.Message);}};
        };
        rtc.VideoTrackRemoved+=(transceiver,track)=>State?.Invoke(id,(transceiver.MlineIndex==1?"screen":"camera")+"-off");
        try {
            await rtc.InitializeAsync(new PeerConnectionConfiguration{IceServers=[],IceTransportType=IceTransportType.All,BundlePolicy=BundlePolicy.MaxBundle});
            ConfigureVideoRate(rtc);
            // Only the offerer creates transceivers. On this native engine the answerer must
            // bind the ones materialized by SetRemoteDescription; pre-creating another set
            // leaves the offered transceivers recvonly and silently prevents reverse media.
            if(IsOfferer(id)){
                p.Camera=rtc.AddTransceiver(MediaKind.Video,new(){Name="camera",InitialDesiredDirection=Transceiver.Direction.SendReceive});
                p.Screen=rtc.AddTransceiver(MediaKind.Video,new(){Name="screen",InitialDesiredDirection=Transceiver.Direction.SendReceive});
                AttachVideo(p);
            }
            AttachVoice(p);
            peers.TryAdd(id,p);
            await SendToPeer(id,"media",Sharing?"screen-on":"screen-off");await SendToPeer(id,"media",CameraOn?"camera-on":"camera-off");
            if(IsOfferer(id)){Offer(p);await p.Voice!.Offer();}
            return p;
        }catch{p.Dispose();throw;}
    }
    bool IsOfferer(string id)=>string.CompareOrdinal(connection.ConnectionId,id)<0;
    Task SendToPeer(string id,string kind,string payload)=>channel!=null&&peers.ContainsKey(id)?connection.Signal(id,kind,payload):Task.CompletedTask;
    void Offer(Peer p){if(p.WaitingAnswer){p.Dirty=true;return;}p.WaitingAnswer=true;if(!p.Rtc.CreateOffer()){p.WaitingAnswer=false;throw new Exception("Não foi possível negociar a chamada.");}}
    async Task Receive(RtcSignal signal)
    {
        if(channel==null)return;
        if(!peers.TryGetValue(signal.From,out var p)){
            // Cross-connection hub messages can overtake the roster. Only consume
            // buffered signals after an authenticated roster includes this connection.
            if(earlySignals.Count<8){if(!earlySignals.TryGetValue(signal.From,out var queued))earlySignals[signal.From]=queued=[];if(queued.Count<128)queued.Add(signal);}
            return;
        }
        if(signal.Kind=="media"){State?.Invoke(signal.From,signal.Payload);return;}
        if(signal.Kind=="audio-reset"){await ResetVoice(p);return;}
        if(signal.Kind.StartsWith("audio-")){if(signal.Kind=="audio-offer"&&IsOfferer(signal.From))return;await p.Voice!.Receive(signal.Kind,signal.Payload);return;}
        switch(signal.Kind){
            case "offer":
                if(IsOfferer(signal.From))return;
                await p.Rtc.SetRemoteDescriptionAsync(new SdpMessage{Type=SdpMessageType.Offer,Content=signal.Payload});p.RemoteSet=true;
                p.Camera=p.Rtc.Transceivers.Single(t=>t.MlineIndex==0&&t.MediaKind==MediaKind.Video);
                p.Screen=p.Rtc.Transceivers.Single(t=>t.MlineIndex==1&&t.MediaKind==MediaKind.Video);
                p.Camera.DesiredDirection=p.Screen.DesiredDirection=Transceiver.Direction.SendReceive;
                FlushIce(p);AttachVideo(p);
                if(!p.Rtc.CreateAnswer())throw new Exception("Não foi possível responder à chamada.");break;
            case "answer":
                await p.Rtc.SetRemoteDescriptionAsync(new SdpMessage{Type=SdpMessageType.Answer,Content=signal.Payload});p.RemoteSet=true;FlushIce(p);p.WaitingAnswer=false;if(p.Dirty){p.Dirty=false;Offer(p);}break;
            case "ice":
                var ice=JsonSerializer.Deserialize<IceCandidate>(signal.Payload,json)??throw new Exception("ICE inválido.");if(p.RemoteSet)p.Rtc.AddIceCandidate(ice);else if(p.Ice.Count<128)p.Ice.Add(ice);break;
            case "renegotiate":if(IsOfferer(signal.From))Offer(p);break;
        }
    }
    static void FlushIce(Peer p){foreach(var ice in p.Ice)p.Rtc.AddIceCandidate(ice);p.Ice.Clear();}
    void AttachVoice(Peer p){p.Voice=new AudioPeer(audio!, (kind,payload)=>SendToPeer(p.Id,kind,payload),payload=>Run(()=>SendToPeer(p.Id,"audio-ice",payload)));p.Voice.SetVolume(VolumeForUser?.Invoke(p.UserId)??100);p.Voice.Error+=message=>Error?.Invoke(message);p.Voice.Changed+=()=>Changed?.Invoke();}
    async Task ResetVoice(Peer p){p.Voice?.Dispose();AttachVoice(p);if(IsOfferer(p.Id))await p.Voice!.Offer();Changed?.Invoke();}
    public async Task RetryAudio(){await serial.WaitAsync();try{foreach(var p in peers.Values){await connection.Signal(p.Id,"audio-reset","");await ResetVoice(p);}}finally{serial.Release();}}
    public async Task SetPeerVolume(long user,int percent){await serial.WaitAsync();try{foreach(var p in peers.Values.Where(p=>p.UserId==user))p.Voice?.SetVolume(Math.Clamp(percent,0,200));}finally{serial.Release();}}
    void AttachVideo(Peer p)
    {
        if(p.CameraTrack==null){p.CameraTrack=LocalVideoTrack.CreateFromSource(cameraFrames!.Source,new(){trackName="camera-"+Guid.NewGuid().ToString("N")});p.CameraTrack.Enabled=camera!=null;p.Camera.LocalVideoTrack=p.CameraTrack;}
        if(p.ScreenTrack==null){p.ScreenTrack=LocalVideoTrack.CreateFromSource(screenFrames!.Source,new(){trackName="screen-"+Guid.NewGuid().ToString("N")});p.ScreenTrack.Enabled=desktop!=null;p.Screen.LocalVideoTrack=p.ScreenTrack;}
    }
    async Task Renegotiate(){foreach(var p in peers.Values){if(IsOfferer(p.Id))Offer(p);else await connection.Signal(p.Id,"renegotiate","");}Changed?.Invoke();}
    public async Task ToggleMute(){await serial.WaitAsync();try{
        if(!InCall)throw new Exception("Entre em um canal de voz.");
        if(Muted)audio!.StartCapture(InputDeviceId);
        Muted=!Muted;audio!.Mute(Muted);Changed?.Invoke();
    }finally{serial.Release();}}
    public async Task ToggleDeafen(){await serial.WaitAsync();try{Deafened=!Deafened;audio?.Deafen(Deafened);Changed?.Invoke();}finally{serial.Release();}}
    public async Task SelectAudio(string? input,string? output){await serial.WaitAsync();try{if(InCall){try{audio!.Configure(input,output,!Muted);}catch{try{audio!.Configure(InputDeviceId,OutputDeviceId,!Muted);}catch(Exception ex){Error?.Invoke("Confira os dispositivos de áudio: "+ex.Message);}throw;}audio.Mute(Muted);audio.Deafen(Deafened);}InputDeviceId=input;OutputDeviceId=output;Changed?.Invoke();}finally{serial.Release();}}
    public async Task StartCamera(string deviceId)
    {
        await serial.WaitAsync();try{
            if(!InCall)throw new Exception("Entre em um canal de voz antes de ligar a câmera.");if(camera!=null)return;
            try{camera=await DeviceVideoTrackSource.CreateAsync(new(){videoDevice=new VideoCaptureDevice{id=deviceId},width=640,height=480,framerate=30});}
            catch{try{camera=await DeviceVideoTrackSource.CreateAsync(new(){videoDevice=new VideoCaptureDevice{id=deviceId}});Error?.Invoke("A câmera usa o formato padrão do dispositivo; 640 × 480 a 30 FPS não estava disponível.");}catch(Exception ex){throw new Exception("Não foi possível abrir a câmera. Confira a permissão para aplicativos da área de trabalho em Configurações → Privacidade → Câmera. "+ex.Message);}}
            cameraPreview=LocalVideoTrack.CreateFromSource(camera,new(){trackName="preview-"+Guid.NewGuid().ToString("N")});
            var output=cameraFrames!;
            cameraPreview.Argb32VideoFrameReady+=frame=>{try{var pixels=FrameCopy.Copy(frame);output.Put(pixels);Frame?.Invoke(connection.ConnectionId??"local","camera",pixels);}catch(Exception ex){Error?.Invoke(ex.Message);}};
            State?.Invoke(connection.ConnectionId??"local","camera-on");
            foreach(var p in peers.Values)if(p.CameraTrack!=null)p.CameraTrack.Enabled=true;await BroadcastState("camera-on");Changed?.Invoke();
        }finally{serial.Release();}
    }
    public async Task StopCamera(){await serial.WaitAsync();try{foreach(var p in peers.Values)if(p.CameraTrack!=null)p.CameraTrack.Enabled=false;cameraPreview?.Dispose();cameraPreview=null;camera?.Dispose();camera=null;cameraFrames?.Clear();await BroadcastState("camera-off");Changed?.Invoke();}finally{serial.Release();}}
    public async Task StartShare(ShareTarget target,int width,int fps)
    {
        await serial.WaitAsync();try{
            if(!InCall)throw new Exception("Entre em um canal de voz antes de compartilhar a tela.");if(desktop!=null)return;
            // Verify the selected source before announcing a track to the other participants.
            _=await Task.Run(()=>DesktopCapture.Capture(target,width));
            var output=screenFrames!;
            shareWidth=width;shareFps=fps;desktop=new DesktopCapture();desktop.Preview+=pixels=>{output.Put(pixels);Frame?.Invoke(connection.ConnectionId??"local","screen",pixels);};
            State?.Invoke(connection.ConnectionId??"local","screen-on");
            desktop.Error+=message=>{Error?.Invoke(message);Run(StopShareCore);};desktop.Start(target,width,fps);
            foreach(var p in peers.Values){ConfigureVideoRate(p.Rtc);if(p.ScreenTrack!=null)p.ScreenTrack.Enabled=true;}await BroadcastState("screen-on");Changed?.Invoke();
        }finally{serial.Release();}
    }
    async Task BroadcastState(string state){foreach(var p in peers.Values)await connection.Signal(p.Id,"media",state);}
    async Task StopShareCore(){foreach(var p in peers.Values)if(p.ScreenTrack!=null)p.ScreenTrack.Enabled=false;desktop?.Dispose();desktop=null;foreach(var p in peers.Values)ConfigureVideoRate(p.Rtc);screenFrames?.Clear();await BroadcastState("screen-off");State?.Invoke(connection.ConnectionId??"local","screen-off");Changed?.Invoke();}
    public async Task StopShare(){await serial.WaitAsync();try{await StopShareCore();}finally{serial.Release();}}
    public async Task Leave(){await serial.WaitAsync();try{await StopLocal();await connection.LeaveCall();Changed?.Invoke();}finally{serial.Release();}}
    Task StopLocal(){channel=null;foreach(var p in peers.Values)p.Dispose();peers.Clear();earlySignals.Clear();cameraPreview?.Dispose();cameraPreview=null;camera?.Dispose();camera=null;desktop?.Dispose();desktop=null;cameraFrames?.Dispose();cameraFrames=null;screenFrames?.Dispose();screenFrames=null;audio?.Dispose();audio=null;Participants?.Invoke(new(0,[]));return Task.CompletedTask;}
    public async ValueTask DisposeAsync(){connection.Roster-=OnRoster;connection.SignalReceived-=OnSignal;connection.Interrupted-=OnInterrupted;connection.CallEnded-=OnCallEnded;await serial.WaitAsync();try{disposed=true;await StopLocal();work.Writer.TryComplete();}finally{serial.Release();}await worker;}
}
