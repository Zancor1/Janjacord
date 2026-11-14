namespace JanjaCord.Server.Networking;
public sealed class PresenceDirectory
{
    readonly System.Collections.Concurrent.ConcurrentDictionary<string,long> sessions=new();
    public void Add(string connection,long user)=>sessions[connection]=user;
    public long? Remove(string connection)=>sessions.TryRemove(connection,out var user)?user:null;
    public bool Online(long user)=>sessions.Values.Contains(user);
}
