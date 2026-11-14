using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JanjaCord.Shared;
namespace JanjaCord.Client.Services;
public sealed record PendingDirect(string ClientId,long LocalId,long PeerId,string Text,DateTimeOffset SentAt,string? Error=null);
public sealed class ConversationCache
{
    public List<FriendEntry> Friends {get;set;}=[];
    public Dictionary<long,List<DirectMessage>> Messages {get;set;}=[];
    public List<PendingDirect> Outbox {get;set;}=[];
    string path="";
    public static ConversationCache Load(string network)
    {
        var path=Path.Combine(LocalProfile.DirectoryPath,"cache-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(network)))[..16]+".bin");ConversationCache cache;
        cache=new();foreach(var candidate in new[]{path,path+".bak"})try{cache=JsonSerializer.Deserialize<ConversationCache>(ProtectedData.Unprotect(File.ReadAllBytes(candidate),null,DataProtectionScope.CurrentUser))??new();break;}catch{}
        cache.path=path;return cache;
    }
    public void Put(DirectMessage message,long me)
    {
        var acknowledged=Outbox.RemoveAll(p=>p.ClientId==message.ClientId)>0;
        var peer=message.SenderId==me?message.RecipientId:message.SenderId;if(!Messages.TryGetValue(peer,out var messages))Messages[peer]=messages=[];
        if(messages.Any(m=>m.Id==message.Id)){if(acknowledged)Save();return;}messages.Add(message);Messages[peer]=messages.OrderBy(m=>m.Id).TakeLast(100).ToList();Save();
    }
    public PendingDirect Enqueue(long peer,string text){var localId=Outbox.Count==0?-1:Outbox.Min(p=>p.LocalId)-1;var pending=new PendingDirect(Guid.NewGuid().ToString(),localId,peer,text,DateTimeOffset.UtcNow);Outbox.Add(pending);Save();return pending;}
    public void Save()
    {
        if(path.Length==0)return;Directory.CreateDirectory(Path.GetDirectoryName(path)!);var bytes=ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(this),null,DataProtectionScope.CurrentUser);var temporary=path+".tmp";File.WriteAllBytes(temporary,bytes);if(File.Exists(path))File.Copy(path,path+".bak",true);File.Move(temporary,path,true);
    }
}
