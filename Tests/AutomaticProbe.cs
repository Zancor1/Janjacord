using System.Security.Cryptography;
using JanjaCord.Client.Networking;
using JanjaCord.Client.Services;
using JanjaCord.Shared;
public static class AutomaticProbe
{
    public static async Task Run()
    {
        var previous=Environment.GetEnvironmentVariable("JANJACORD_PROFILE_DIR");var import=Environment.GetEnvironmentVariable("JANJACORD_DISABLE_IMPORT");var root=Path.Combine(Path.GetTempPath(),"JanjaCord-auto-"+Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("JANJACORD_DISABLE_IMPORT","1");
        try{
            Environment.SetEnvironmentVariable("JANJACORD_PROFILE_DIR",Path.Combine(root,"a"));
            await using var a=new AutomaticNetwork();var first=await a.Connect(new());if(!a.Hosting)throw new Exception("Teste isolado não hospedou a rede.");
            var discovered=await LanDiscovery.Find(TimeSpan.FromMilliseconds(1000));if(!discovered.Any(d=>d.Host.Id==first.Host.Id))throw new Exception("Host não foi encontrado por UDP.");
            Console.WriteLine("PASS: host inicia dentro do cliente e é descoberto por UDP sem Server.exe");
            Environment.SetEnvironmentVariable("JANJACORD_PROFILE_DIR",Path.Combine(root,"b"));
            await using var b=new AutomaticNetwork();var second=await b.Connect(new());if(b.Hosting||second.Host.Id!=first.Host.Id)throw new Exception("Segundo cliente criou outra rede.");
            await using var ca=new Connection();await using var cb=new Connection();
            await ca.LoginDevice(first.Address,new("Automatic Alice",Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),null),first.Host.Fingerprint);
            await cb.LoginDevice(second.Address,new("Automatic Bob",Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),null),second.Host.Fingerprint);
            await ca.Post("/api/friends",new FriendRequest("Automatic Bob"));await cb.Post($"/api/friends/{ca.Session!.UserId}/accept",new{});
            var received=new TaskCompletionSource<DirectMessage>(TaskCreationOptions.RunContinuationsAsynchronously);cb.DirectMessageReceived+=message=>received.TrySetResult(message);
            await ca.SendDirect(cb.Session!.UserId,"Mensagem pela rede automática");await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Console.WriteLine("PASS: segundo cliente encontra a mesma rede e recebe DM real de um amigo");
        }finally{Environment.SetEnvironmentVariable("JANJACORD_PROFILE_DIR",previous);Environment.SetEnvironmentVariable("JANJACORD_DISABLE_IMPORT",import);}
    }
}
