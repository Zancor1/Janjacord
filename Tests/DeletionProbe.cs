using System.Net;
using System.Net.Http.Json;
using JanjaCord.Shared;
using Microsoft.AspNetCore.SignalR.Client;
public static class DeletionProbe
{
    public static async Task Run(HttpClient owner,HttpClient member,HubConnection a,HubConnection b,long preserved)
    {
        using var response=await owner.PostAsJsonAsync("/api/communities",new CreateCommunity("Excluir "+Guid.NewGuid().ToString("N")[..6]));response.EnsureSuccessStatusCode();var community=(await response.Content.ReadFromJsonAsync<Community>())!;
        (await member.PostAsJsonAsync($"/api/communities/{community.Id}/join",new{})).EnsureSuccessStatusCode();
        using var channelResponse=await owner.PostAsJsonAsync($"/api/communities/{community.Id}/channels",new CreateChannel("temporario","text"));channelResponse.EnsureSuccessStatusCode();var channel=(await channelResponse.Content.ReadFromJsonAsync<Channel>())!;
        await a.InvokeAsync("Send",new SendMessage(channel.Id,"Mensagem que deve ser removida com o servidor."));
        using(var refused=await member.DeleteAsync($"/api/communities/{community.Id}"))if(refused.StatusCode!=HttpStatusCode.Forbidden)throw new Exception("Membro conseguiu excluir servidor de outro usuário.");
        Console.WriteLine("PASS: somente o dono pode excluir o servidor");
        var deleted=new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);using var handler=b.On<long>("CommunityDeleted",id=>deleted.TrySetResult(id));
        (await owner.DeleteAsync($"/api/communities/{community.Id}")).EnsureSuccessStatusCode();
        if(await deleted.Task.WaitAsync(TimeSpan.FromSeconds(10))!=community.Id)throw new Exception("Evento de exclusão incorreto.");
        var list=await member.GetFromJsonAsync<List<Community>>("/api/communities")??throw new Exception("Lista vazia.");if(list.Any(c=>c.Id==community.Id)||!list.Any(c=>c.Id==preserved))throw new Exception("Exclusão afetou servidores incorretos.");
        if((await member.GetAsync($"/api/channels/{channel.Id}/messages")).StatusCode!=HttpStatusCode.Forbidden)throw new Exception("Canal excluído ainda acessível.");
        if((await owner.PostAsJsonAsync($"/api/communities/{community.Id}/join",new{})).StatusCode!=HttpStatusCode.NotFound)throw new Exception("Servidor excluído ainda aceita entrada.");
        Console.WriteLine("PASS: exclusão remove canais, notifica outro cliente e preserva os demais servidores");
        using var next=await owner.PostAsJsonAsync("/api/communities",new CreateCommunity("Novo após exclusão"));next.EnsureSuccessStatusCode();var newer=(await next.Content.ReadFromJsonAsync<Community>())!;
        if(newer.Id<=community.Id)throw new Exception("Identificador de servidor excluído foi reutilizado.");
        using var nextChannel=await owner.PostAsJsonAsync($"/api/communities/{newer.Id}/channels",new CreateChannel("novo","text"));nextChannel.EnsureSuccessStatusCode();var newerChannel=(await nextChannel.Content.ReadFromJsonAsync<Channel>())!;
        if(newerChannel.Id<=channel.Id)throw new Exception("Identificador de canal excluído foi reutilizado.");
        Console.WriteLine("PASS: IDs excluídos não são reutilizados em grupos SignalR futuros");
    }
}
