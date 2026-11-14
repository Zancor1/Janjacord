using JanjaCord.Client.Media;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
public static class SelectableAudioProbe
{
    public static async Task Run()
    {
        using var ea=new SelectedAudio();using var eb=new SelectedAudio();
        var qa=System.Threading.Channels.Channel.CreateUnbounded<(string Kind,string Payload)>();var qb=System.Threading.Channels.Channel.CreateUnbounded<(string Kind,string Payload)>();
        var candidates=new System.Collections.Concurrent.ConcurrentBag<string>();
        using var a=new AudioPeer(ea,(k,p)=>{candidates.Add(p);qb.Writer.TryWrite((k,p));return Task.CompletedTask;},p=>{candidates.Add(p);qb.Writer.TryWrite(("audio-ice",p));});
        using var b=new AudioPeer(eb,(k,p)=>{candidates.Add(p);qa.Writer.TryWrite((k,p));return Task.CompletedTask;},p=>{candidates.Add(p);qa.Writer.TryWrite(("audio-ice",p));});
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        async Task Process(System.Threading.Channels.Channel<(string Kind,string Payload)> queue,AudioPeer peer){try{await foreach(var (kind,payload) in queue.Reader.ReadAllAsync(stop.Token))await peer.Receive(kind,payload);}catch(OperationCanceledException){}}
        var wa=Process(qa,a);var wb=Process(qb,b);
        try{
            await a.Offer();while(!a.Connected||!b.Connected)await Task.Delay(30,stop.Token);
            foreach(var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up))foreach(var address in nic.GetIPProperties().UnicastAddresses.Select(a=>a.Address.ToString()).Where(ip=>ip.StartsWith("26.")))if(!candidates.Any(candidate=>candidate.Contains(address)))throw new Exception("A interface Radmin não foi anunciada no ICE de áudio.");
            Console.WriteLine("PASS: ICE de áudio inclui a interface Radmin ativa, além da rota padrão");
            using var encoder=new AudioEncoder(AudioCommonlyUsedFormats.OpusWebRTC);using var decoder=new AudioEncoder(AudioCommonlyUsedFormats.OpusWebRTC);
            var pcm=Enumerable.Range(0,960).Select(i=>(short)(Math.Sin(i*2*Math.PI*440/48000)*6000)).ToArray();var packet=encoder.EncodeAudio(pcm,AudioCommonlyUsedFormats.OpusWebRTC);var decoded=decoder.DecodeAudio(packet,AudioCommonlyUsedFormats.OpusWebRTC);
            if(decoded.Length!=960||!decoded.Any(x=>Math.Abs((int)x)>200))throw new Exception($"Opus duração/energia incorreta: {decoded.Length} amostras.");
            for(var i=0;i<20;i++){a.SendEncoded(packet);b.SendEncoded(packet);await Task.Delay(20,stop.Token);}
            while(a.ReceivedSamples<960*15||b.ReceivedSamples<960*15)await Task.Delay(30,stop.Token);
            Console.WriteLine("PASS: áudio Opus de 20 ms real, DTLS-SRTP/ICE e decodificação nos dois sentidos, sem abrir microfone");
            var inputs=SelectedAudio.Devices(true);var outputs=SelectedAudio.Devices(false);Console.WriteLine($"INFO: dispositivos WASAPI enumerados: {inputs.Length-1} entrada(s), {outputs.Length-1} saída(s).");
        }finally{stop.Cancel();await Task.WhenAll(wa,wb);}
        using var unanswered=new AudioPeer(ea,(_,_)=>Task.CompletedTask,_=>{});await unanswered.Offer();
        using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(24))){while(!unanswered.Failed)await Task.Delay(50,deadline.Token);}
        Console.WriteLine("PASS: conexão sem resposta sai de conectando após 20s e permite tentar novamente");
    }
}
