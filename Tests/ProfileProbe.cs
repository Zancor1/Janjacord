using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using JanjaCord.Client.Services;
using JanjaCord.Shared;
public static class ProfileProbe
{
    public static async Task Run(HttpClient a,HttpClient b,HubConnection peer,Session alice,Session bob)
    {
        using var bitmap=new System.Drawing.Bitmap(12,12);using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.Clear(System.Drawing.Color.CornflowerBlue);using var bytes=new MemoryStream();bitmap.Save(bytes,System.Drawing.Imaging.ImageFormat.Png);var photo=Convert.ToBase64String(bytes.ToArray());
        var name="Perfil_"+Guid.NewGuid().ToString("N")[..8];var changed=new TaskCompletionSource<MemberProfile>(TaskCreationOptions.RunContinuationsAsynchronously);using var subscription=peer.On<MemberProfile>("ProfileChanged",p=>changed.TrySetResult(p));
        using var response=await a.PutAsJsonAsync("/api/profile",new UpdateProfile(name,photo));response.EnsureSuccessStatusCode();var updated=(await response.Content.ReadFromJsonAsync<MemberProfile>())!;
        if(updated.Id!=alice.UserId||updated.Name!=name||updated.Avatar!=photo)throw new Exception("Editar perfil alterou a identidade ou não salvou os dados.");
        var notification=await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));if(notification!=updated)throw new Exception("Outro cliente não recebeu o perfil atualizado.");
        var friends=await b.GetFromJsonAsync<List<FriendEntry>>("/api/friends");if(friends?.SingleOrDefault(f=>f.Id==alice.UserId) is not {} friend||friend.Name!=name||friend.Avatar!=photo)throw new Exception("Amizade não foi preservada com o novo perfil.");
        Console.WriteLine("PASS: editar nome/foto mantém a conta e a amizade e sincroniza o outro cliente");
        using var refused=await a.PutAsJsonAsync("/api/profile",new UpdateProfile(bob.Name,null));if(refused.StatusCode!=HttpStatusCode.Conflict)throw new Exception("Edição aceitou nome usado por outra pessoa.");
        friends=await b.GetFromJsonAsync<List<FriendEntry>>("/api/friends");var retained=friends!.Single(f=>f.Id==alice.UserId);if(retained.Name!=name||retained.Avatar!=photo)throw new Exception("Edição recusada alterou o nome ou a foto.");
        Console.WriteLine("PASS: nome repetido é recusado sem perder a foto nem alterar o perfil anterior");
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory,"profile-persistence.json"),System.Text.Json.JsonSerializer.Serialize(new{Peer=alice.UserId,Name=name,Avatar=photo,Token=bob.Token}));
        var previous=Environment.GetEnvironmentVariable("JANJACORD_PROFILE_DIR");try{
            var path=Path.Combine(Path.GetTempPath(),"JanjaCord-profile-"+Guid.NewGuid().ToString("N"));Environment.SetEnvironmentVariable("JANJACORD_PROFILE_DIR",path);
            var profile=new LocalProfile{Name=name,Avatar=photo,UserId=alice.UserId};var key=profile.DeviceKey();profile.Save();profile.Save();
            var restored=LocalProfile.Load();if(restored.Name!=name||restored.Avatar!=photo||restored.UserId!=alice.UserId||restored.DeviceKey()!=key)throw new Exception("Primeiro perfil não persistiu.");
            await File.WriteAllTextAsync(Path.Combine(path,"profile.json"),"arquivo incompleto");restored=LocalProfile.Load();if(restored.UserId!=alice.UserId||restored.DeviceKey()!=key)throw new Exception("Backup não recuperou a mesma identidade.");
            Console.WriteLine("PASS: primeiro cadastro salva nome/foto/chave e backup recupera a mesma conta");
        }finally{Environment.SetEnvironmentVariable("JANJACORD_PROFILE_DIR",previous);}
    }
}
