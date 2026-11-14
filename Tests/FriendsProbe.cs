using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR.Client;
using JanjaCord.Shared;
public static class FriendsProbe
{
    public static async Task Run(HttpClient a,HttpClient b,HubConnection ha,HubConnection hb,Session alice,Session bob)
    {
        using var sent=await a.PostAsJsonAsync("/api/friends",new FriendRequest(bob.Name));sent.EnsureSuccessStatusCode();var outgoing=(await sent.Content.ReadFromJsonAsync<FriendEntry>())!;
        if(outgoing.Id!=bob.UserId||outgoing.State!="pending"||outgoing.Incoming)throw new Exception("Solicitação de amizade incorreta.");
        var incoming=await b.GetFromJsonAsync<List<FriendEntry>>("/api/friends");if(incoming?.SingleOrDefault(f=>f.Id==alice.UserId) is not {Incoming:true,State:"pending",Online:true})throw new Exception("Solicitação não chegou ao amigo.");
        Console.WriteLine("PASS: adicionar amigo cria solicitação/DM nos dois clientes e mostra presença real");
        try{await ha.InvokeAsync<DirectMessage>("SendDirect",new SendDirectMessage(bob.UserId,"não permitido",Guid.NewGuid().ToString()));throw new Exception("Mensagem antes da aceitação foi permitida.");}catch(Microsoft.AspNetCore.SignalR.HubException){}
        (await b.PostAsJsonAsync($"/api/friends/{alice.UserId}/accept",new{})).EnsureSuccessStatusCode();
        var received=new TaskCompletionSource<DirectMessage>(TaskCreationOptions.RunContinuationsAsynchronously);using var subscription=hb.On<DirectMessage>("DirectMessage",m=>received.TrySetResult(m));
        var clientId=Guid.NewGuid().ToString();var message=await ha.InvokeAsync<DirectMessage>("SendDirect",new SendDirectMessage(bob.UserId,"DM real persistida",clientId));
        if((await received.Task.WaitAsync(TimeSpan.FromSeconds(10))).Id!=message.Id)throw new Exception("DM não chegou ao outro cliente.");
        var repeated=await ha.InvokeAsync<DirectMessage>("SendDirect",new SendDirectMessage(bob.UserId,"DM real persistida",clientId));if(repeated.Id!=message.Id)throw new Exception("Reenvio duplicou a mensagem.");
        var history=await b.GetFromJsonAsync<List<DirectMessage>>($"/api/direct/{alice.UserId}/messages");if(history!.Count(m=>m.Id==message.Id)!=1)throw new Exception("Histórico de DM incorreto.");
        Console.WriteLine("PASS: amizade aceita, DM real recebida/salva e reenvio idempotente");
        await hb.StopAsync();
        for(var i=0;i<30;i++){if((await a.GetFromJsonAsync<List<FriendEntry>>("/api/friends"))!.Single(f=>f.Id==bob.UserId).Online==false)break;await Task.Delay(50);}
        var offline=await ha.InvokeAsync<DirectMessage>("SendDirect",new SendDirectMessage(bob.UserId,"Mensagem para amigo offline",Guid.NewGuid().ToString()));
        if(!(await b.GetFromJsonAsync<List<DirectMessage>>($"/api/direct/{alice.UserId}/messages"))!.Any(m=>m.Id==offline.Id))throw new Exception("Mensagem offline não foi guardada.");
        await hb.StartAsync();Console.WriteLine("PASS: amigo offline recebe mensagem guardada no host quando volta");
        using var duplicate=new HttpClient{BaseAddress=a.BaseAddress};using var denied=await duplicate.PostAsJsonAsync("/api/auth/device",new DeviceIdentity("  "+bob.Name.ToUpperInvariant()+"  ",Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),null));if(denied.StatusCode!=HttpStatusCode.Conflict)throw new Exception("Nome repetido foi permitido.");
        Console.WriteLine("PASS: nomes repetidos são recusados, incluindo caixa e espaços nas pontas");
        using var stranger=new HttpClient{BaseAddress=a.BaseAddress};using var registered=await stranger.PostAsJsonAsync("/api/auth/register",new Credentials("outsider_"+Guid.NewGuid().ToString("N")[..8],"Test-password-2941"));registered.EnsureSuccessStatusCode();var identity=(await registered.Content.ReadFromJsonAsync<Session>())!;stranger.DefaultRequestHeaders.Authorization=new("Bearer",identity.Token);
        if((await stranger.GetAsync($"/api/direct/{alice.UserId}/messages")).StatusCode!=HttpStatusCode.Forbidden)throw new Exception("Terceiro leu DM sem amizade.");
        Console.WriteLine("PASS: terceiro sem amizade não consegue acessar histórico privado");
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory,"direct-persistence.json"),System.Text.Json.JsonSerializer.Serialize(new{Peer=alice.UserId,Message=message.Id,Token=bob.Token}));
    }
}
