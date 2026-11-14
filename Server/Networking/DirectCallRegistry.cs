using JanjaCord.Shared;
namespace JanjaCord.Server.Networking;
public sealed class DirectCallRegistry
{
    readonly object gate=new();
    readonly Dictionary<long,(DirectCallInfo Info,DateTimeOffset Created)> calls=[];
    long next;
    public DirectCallInfo Begin(MemberProfile caller,MemberProfile callee,string connection)
    {lock(gate){if(calls.Values.Any(c=>c.Info.Caller.Id==caller.Id||c.Info.Callee.Id==caller.Id||c.Info.Caller.Id==callee.Id||c.Info.Callee.Id==callee.Id))throw new InvalidOperationException("Uma das pessoas já está em uma chamada.");var info=new DirectCallInfo(--next,caller,callee,connection,null,"ringing");calls[info.Id]=(info,DateTimeOffset.UtcNow);return info;}}
    public DirectCallInfo Accept(long id,long user,string connection)
    {lock(gate){if(!calls.TryGetValue(id,out var current)||current.Info.Callee.Id!=user||current.Info.State!="ringing")throw new InvalidOperationException("Esta chamada não está mais disponível.");var info=current.Info with{CalleeConnection=connection,State="active"};calls[id]=(info,current.Created);return info;}}
    public bool Allowed(long id,long user,string connection)
    {lock(gate)return calls.TryGetValue(id,out var current)&&current.Info.State=="active"&&((current.Info.Caller.Id==user&&current.Info.CallerConnection==connection)||(current.Info.Callee.Id==user&&current.Info.CalleeConnection==connection));}
    public DirectCallInfo? Get(long id){lock(gate)return calls.TryGetValue(id,out var c)?c.Info:null;}
    public DirectCallInfo? End(long id,string reason){lock(gate)return calls.Remove(id,out var current)?current.Info with{State="ended",Reason=reason}:null;}
    public long[] ForConnection(string connection){lock(gate)return calls.Values.Where(c=>c.Info.CallerConnection==connection||c.Info.CalleeConnection==connection).Select(c=>c.Info.Id).ToArray();}
    public long[] ForUser(long user){lock(gate)return calls.Values.Where(c=>c.Info.Caller.Id==user||c.Info.Callee.Id==user).Select(c=>c.Info.Id).ToArray();}
    public long[] Expired(){lock(gate)return calls.Values.Where(c=>c.Info.State=="ringing"&&DateTimeOffset.UtcNow-c.Created>TimeSpan.FromSeconds(35)).Select(c=>c.Info.Id).ToArray();}
}
