using System.Text.Json;
using NAudio.Wave;
using SIPSorcery.Net;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
namespace JanjaCord.Client.Media;
public sealed class AudioPeer:IDisposable
{
    // Radmin/LAN can use a different NIC from the Internet route. Both peers must
    // advertise those addresses, rather than only the default Ethernet interface.
    readonly RTCPeerConnection rtc=new(new RTCConfiguration{iceServers=[],X_ICEIncludeAllInterfaceAddresses=true});
    readonly AudioEncoder decoder=new(AudioCommonlyUsedFormats.OpusWebRTC);
    readonly object gate=new();readonly List<RTCIceCandidateInit> pending=[];
    readonly SelectedAudio engine;readonly BufferedWaveProvider buffer;
    readonly Func<string,string,Task> signal;bool remote,closed,connected;
    public event Action<string>? Error;
    public event Action? Changed;
    public long ReceivedSamples {get;private set;}
    public bool Connected {get {lock(gate)return connected;}}
    public bool Failed {get;private set;}
    readonly CancellationTokenSource lifetime=new();
    public AudioPeer(SelectedAudio engine,Func<string,string,Task> signal,Action<string> ice)
    {
        this.engine=engine;this.signal=signal;buffer=engine.AddRemote();
        rtc.addTrack(new MediaStreamTrack(new List<AudioFormat>{AudioCommonlyUsedFormats.OpusWebRTC},MediaStreamStatusEnum.SendRecv));
        rtc.onicecandidate+=candidate=>{if(candidate!=null)ice(candidate.toJSON());};
        rtc.onconnectionstatechange+=state=>{lock(gate){connected=state==RTCPeerConnectionState.connected;Failed=state is RTCPeerConnectionState.failed or RTCPeerConnectionState.disconnected;if(connected)Failed=false;}Changed?.Invoke();if(state==RTCPeerConnectionState.failed)Error?.Invoke("Não foi possível conectar o áudio. Confira o firewall e a VPN e tente reconectar a voz.");};
        rtc.OnRtpPacketReceived+=(_,kind,packet)=>{if(kind!=SDPMediaTypesEnum.audio)return;try{lock(gate){if(closed)return;var pcm=decoder.DecodeAudio(packet.Payload,AudioCommonlyUsedFormats.OpusWebRTC);var bytes=new byte[pcm.Length*2];Buffer.BlockCopy(pcm,0,bytes,0,bytes.Length);buffer.AddSamples(bytes,0,bytes.Length);ReceivedSamples+=pcm.Length;}}catch(Exception ex){Error?.Invoke("Falha ao receber áudio: "+ex.Message);}};
        engine.Encoded+=SendEncoded;
        _=WatchConnection();
    }
    async Task WatchConnection(){try{await Task.Delay(TimeSpan.FromSeconds(20),lifetime.Token);lock(gate){if(closed||connected)return;Failed=true;}Changed?.Invoke();Error?.Invoke("O áudio não conectou em 20 segundos. Use Reconectar voz e confira se os dois clientes estão liberados no firewall.");}catch(OperationCanceledException){}}
    public void SendEncoded(byte[] payload){lock(gate){if(!closed&&connected)rtc.SendAudio(960,payload);}}
    public void SetVolume(int percent)=>engine.SetRemoteVolume(buffer,percent);
    public async Task Offer(){var offer=rtc.createOffer(null);await rtc.setLocalDescription(offer);await signal("audio-offer",offer.sdp);}
    public async Task Receive(string kind,string payload)
    {
        if(closed)return;
        if(kind=="audio-ice"){var ice=JsonSerializer.Deserialize<RTCIceCandidateInit>(payload)??throw new Exception("ICE de áudio inválido.");if(remote)rtc.addIceCandidate(ice);else if(pending.Count<128)pending.Add(ice);return;}
        var type=kind=="audio-offer"?RTCSdpType.offer:RTCSdpType.answer;
        var result=rtc.setRemoteDescription(new RTCSessionDescriptionInit{type=type,sdp=payload});if(result!=SetDescriptionResultEnum.OK)throw new Exception("Negociação de áudio recusada: "+result);
        remote=true;foreach(var ice in pending)rtc.addIceCandidate(ice);pending.Clear();
        if(type==RTCSdpType.offer){var answer=rtc.createAnswer(null);await rtc.setLocalDescription(answer);await signal("audio-answer",answer.sdp);}
    }
    public void Dispose(){engine.Encoded-=SendEncoded;lock(gate){if(closed)return;closed=true;lifetime.Cancel();connected=false;rtc.Close("Chamada encerrada");rtc.Dispose();decoder.Dispose();}engine.RemoveRemote(buffer);lifetime.Dispose();}
}
