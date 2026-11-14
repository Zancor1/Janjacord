using JanjaCord.Server.Database;
using JanjaCord.Shared;
namespace JanjaCord.Server.Networking;
public sealed class CallDirectory
{
    readonly object gate=new();
    readonly Dictionary<string,(long Channel,CallPeer Peer)> peers=[];
    public CallRoster Join(string connection,long channel,CallPeer peer,Func<bool> authorized)
    { lock(gate) {if(peers.Any(p=>p.Key!=connection&&p.Value.Peer.UserId==peer.UserId))throw new InvalidOperationException("Esta conta j\u00e1 participa de uma chamada em outro dispositivo.");if(!authorized())throw new InvalidOperationException("Canal indisponível.");if(peers.Values.Count(p=>p.Channel==channel)>=10&&!peers.ContainsKey(connection))throw new InvalidOperationException("Sala cheia: limite de 10 participantes nesta versão.");peers[connection]=(channel,peer);return Snapshot(channel);} }
    public (string Connection,long Channel)[] CloseChannels(IEnumerable<long> channels)
    {lock(gate){var ids=channels.ToHashSet();var removed=peers.Where(p=>ids.Contains(p.Value.Channel)).Select(p=>(p.Key,p.Value.Channel)).ToArray();foreach(var (connection,_) in removed)peers.Remove(connection);return removed;}}
    public long? Leave(string connection){lock(gate){if(peers.Remove(connection,out var value))return value.Channel;return null;}}
    public CallRoster Snapshot(long channel){lock(gate)return new(channel,peers.Values.Where(p=>p.Channel==channel).Select(p=>p.Peer).ToArray());}
    public bool SameRoom(string a,string b){lock(gate)return peers.TryGetValue(a,out var x)&&peers.TryGetValue(b,out var y)&&x.Channel==y.Channel;}
    public long? Room(string connection){lock(gate)return peers.TryGetValue(connection,out var p)?p.Channel:null;}
    public bool UserBusy(long user){lock(gate)return peers.Values.Any(p=>p.Peer.UserId==user);}
    public CallRoster[] UpdateProfile(MemberProfile profile)
    {lock(gate){var rooms=new HashSet<long>();foreach(var key in peers.Keys.ToArray()){var value=peers[key];if(value.Peer.UserId!=profile.Id)continue;peers[key]=(value.Channel,value.Peer with{Name=profile.Name,Avatar=profile.Avatar});rooms.Add(value.Channel);}return rooms.Select(Snapshot).ToArray();}}
}
