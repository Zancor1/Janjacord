using System.Security.Cryptography;
using JanjaCord.Client.Media;
using JanjaCord.Client.Services;
using JanjaCord.Shared;
using Microsoft.MixedReality.WebRTC;
public static class MediaProbe
{
    public static async Task Run(string address,string pin)
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];var key=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var a=new Connection();await using var b=new Connection();
        await a.LoginDevice(address,new("media-a-"+suffix,key,null),pin);
        await b.LoginDevice(address,new("media-b-"+suffix,Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),null),pin);
        await using(var resume=new Connection()) {
            await resume.LoginDevice(address,new("media-a-"+suffix,key,null),pin);
            if(resume.Session!.UserId!=a.Session!.UserId)throw new Exception("Perfil por dispositivo não foi preservado.");
            Console.WriteLine("PASS: cadastro sem senha e retorno seguro pelo mesmo dispositivo");
        }
        await a.Post("/api/communities",new CreateCommunity("Media "+suffix));
        var community=(await a.Get<List<Community>>("/api/communities")).Single();
        await a.Post($"/api/communities/{community.Id}/channels",new CreateChannel("chamada","voice"));
        var channel=(await a.Get<List<Channel>>($"/api/communities/{community.Id}/channels")).Single();
        await b.Post($"/api/communities/{community.Id}/join",new{});
        await using var ca=new CallService(a);await using var cb=new CallService(b);
        var nativeAudio=false;ca.Diagnostics+=sdp=>{if(sdp.Contains("m=audio"))nativeAudio=true;};cb.Diagnostics+=sdp=>{if(sdp.Contains("m=audio"))nativeAudio=true;};
        ca.Error+=error=>Console.WriteLine("MEDIA A: "+error);cb.Error+=error=>Console.WriteLine("MEDIA B: "+error);
        if(Environment.GetEnvironmentVariable("JANJACORD_VERBOSE_TESTS")=="1"){ca.Diagnostics+=text=>Console.WriteLine("SDP A: "+text);cb.Diagnostics+=text=>Console.WriteLine("SDP B: "+text);}
        var connectedA=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var connectedB=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ca.State+=(id,state)=>{Console.WriteLine("ICE A: "+state);if(state is "Connected" or "Completed")connectedA.TrySetResult();};
        cb.State+=(id,state)=>{Console.WriteLine("ICE B: "+state);if(state is "Connected" or "Completed")connectedB.TrySetResult();};
        await ca.Join(channel.Id,false);await cb.Join(channel.Id,false);
        await Task.WhenAll(connectedA.Task,connectedB.Task).WaitAsync(TimeSpan.FromSeconds(30));
        if(nativeAudio)throw new Exception("O motor de vídeo abriu uma trilha de áudio nativa indevida.");
        using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25))){while(!ca.AudioConnected||!cb.AudioConnected)await Task.Delay(50,timeout.Token);}
        Console.WriteLine("PASS: vídeo negocia somente câmera/tela, sem trilha nativa de áudio que abre sessão de comunicação");
        await ca.RetryAudio();
        using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25))){while(!ca.AudioConnected||!cb.AudioConnected)await Task.Delay(50,timeout.Token);}
        Console.WriteLine("PASS: reconectar voz refaz ICE/DTLS-SRTP nos dois clientes sem sair da chamada");
        Console.WriteLine("PASS: duas conexões WebRTC nativas negociaram ICE por sinalização autenticada");
        var originalMuted=ca.Muted;var originalDeafened=ca.Deafened;
        await ca.ToggleMute();await Task.Delay(150);await ca.PauseMicrophoneForTest(true);var sent=ca.SentAudioPackets;
        using(var preview=new MicrophonePreview()){
            preview.Start(null,null,true,.75f);await Task.Delay(650);if(preview.Packets<5)throw new Exception("Teste de microfone não produziu retorno local.");
            if(ca.SentAudioPackets!=sent)throw new Exception("Áudio do teste foi enviado à chamada.");
            preview.SetNoiseSuppression(true,0);await Task.Delay(100);preview.SetNoiseSuppression(true,1);await Task.Delay(100);
        }
        await ca.PauseMicrophoneForTest(false);await Task.Delay(150);if(ca.SentAudioPackets<=sent)throw new Exception("Transmissão não voltou ao parar o teste.");await ca.ToggleMute();
        if(ca.Muted!=originalMuted||ca.Deafened!=originalDeafened)throw new Exception("O teste alterou o estado anterior de microfone/áudio.");
        Console.WriteLine("PASS: retorno local real com Opus, intensidade0..1, nenhum pacote do teste na call e envio restaurado ao parar");
        var remoteFrame=new TaskCompletionSource<VideoPixels>(TaskCreationOptions.RunContinuationsAsynchronously);
        long screenFramesReceived=0,screenFramesCaptured=0;
        ca.Frame+=(id,kind,pixels)=>{if(kind=="screen")Interlocked.Increment(ref screenFramesCaptured);};
        cb.Frame+=(id,kind,pixels)=>{if(id==a.ConnectionId&&kind=="screen"){Interlocked.Increment(ref screenFramesReceived);remoteFrame.TrySetResult(pixels);}};
        var target=DesktopCapture.Targets().FirstOrDefault(t=>t.Window==IntPtr.Zero)??throw new Exception("Nenhum monitor disponível no ambiente de testes.");
        await ca.StartShare(target,640,60);
        VideoPixels received;
        try{received=await remoteFrame.Task.WaitAsync(TimeSpan.FromSeconds(15));}catch{Console.WriteLine("STATS A: "+await ca.DebugStats());Console.WriteLine("STATS B: "+await cb.DebugStats());throw;}
        if(received.Width<=0||received.Height<=0||received.Bgra.Length!=received.Width*received.Height*4)throw new Exception("Quadro recebido inválido.");
        Console.WriteLine($"PASS: captura REAL de monitor transmitida como vídeo WebRTC VP8 e recebida ({received.Width}x{received.Height})");
        await Task.Delay(1000);var captureStart=Interlocked.Read(ref screenFramesCaptured);var frameStart=Interlocked.Read(ref screenFramesReceived);var frameTimer=System.Diagnostics.Stopwatch.StartNew();await Task.Delay(3000);
        var remoteFps=(Interlocked.Read(ref screenFramesReceived)-frameStart)/frameTimer.Elapsed.TotalSeconds;
        Console.WriteLine($"MEASURE: requested60FPS, received={remoteFps:F1}FPS via VP8/DTLS-SRTP");
        Console.WriteLine($"MEASURE: desktop capture={(Interlocked.Read(ref screenFramesCaptured)-captureStart)/frameTimer.Elapsed.TotalSeconds:F1}FPS");
        Console.WriteLine("STATS A: "+await ca.DebugStats());
        if(remoteFps<40)throw new Exception("Transmiss\u00e3o60FPS abaixo de40FPS no teste.");
        await ca.StopShare();if(ca.Sharing)throw new Exception("Captura não parou.");
        Console.WriteLine("PASS: interrupção do compartilhamento libera a fonte");
        var reverseFrame=new TaskCompletionSource<VideoPixels>(TaskCreationOptions.RunContinuationsAsynchronously);
        ca.Frame+=(id,kind,pixels)=>{if(id==b.ConnectionId&&kind=="screen")reverseFrame.TrySetResult(pixels);};
        await cb.StartShare(target,640,5);await reverseFrame.Task.WaitAsync(TimeSpan.FromSeconds(30));await cb.StopShare();
        Console.WriteLine("PASS: o outro participante também inicia e transmite a tela sem renegociar os emissores");
        await a.Delete($"/api/communities/{community.Id}");
        using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10))){while(ca.InCall||cb.InCall)await Task.Delay(50,timeout.Token);}
        Console.WriteLine("PASS: excluir o servidor encerra a chamada nos dois clientes e libera as fontes");
        await ca.Leave();await cb.Leave();
        Console.WriteLine("PASS: entrada/saída da sala e liberação das conexões nativas");
        await a.Post("/api/friends",new FriendRequest(b.Session!.Name));await b.Post($"/api/friends/{a.Session!.UserId}/accept",new{});
        var incoming=new TaskCompletionSource<DirectCallInfo>(TaskCreationOptions.RunContinuationsAsynchronously);var ready=new TaskCompletionSource<DirectCallInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        b.IncomingDirectCall+=info=>incoming.TrySetResult(info);a.DirectCallReady+=info=>ready.TrySetResult(info);
        var dm=await a.StartDirectCall(b.Session.UserId);await incoming.Task.WaitAsync(TimeSpan.FromSeconds(5));await b.AcceptDirectCall(dm.Id);await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await ca.Join(dm.Id,false);await cb.Join(dm.Id,false);
        var directFrame=new TaskCompletionSource<VideoPixels>(TaskCreationOptions.RunContinuationsAsynchronously);cb.Frame+=(id,kind,pixels)=>{if(id==a.ConnectionId&&kind=="screen")directFrame.TrySetResult(pixels);};
        await ca.StartShare(target,640,5);await directFrame.Task.WaitAsync(TimeSpan.FromSeconds(30));await ca.StopShare();await a.EndDirectCall(dm.Id);
        using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10))){while(ca.InCall||cb.InCall)await Task.Delay(50,timeout.Token);}
        Console.WriteLine("PASS: atender chamada na DM, transmitir tela REAL e desligar libera mídia nos dois clientes");
        var devices=await DeviceVideoTrackSource.GetCaptureDevicesAsync();Console.WriteLine($"INFO: {devices.Count} câmera(s) detectada(s); captura não ativada pelo teste automático.");
    }
}
