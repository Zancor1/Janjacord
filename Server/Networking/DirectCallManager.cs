using JanjaCord.Server.Database;
using JanjaCord.Shared;
using Microsoft.AspNetCore.SignalR;
namespace JanjaCord.Server.Networking;
public sealed class DirectCallManager(Store store,FriendStore friends,PresenceDirectory presence,CallDirectory rooms,DirectCallRegistry registry,IHubContext<ChatHub> hub,CallActivityPublisher activity)
{
    MemberProfile Profile(long user){using var db=store.Open();using var cmd=Store.Command(db,"SELECT u.name,p.avatar FROM users u LEFT JOIN profiles p ON p.userId=u.id WHERE u.id=$u",("$u",user));using var row=cmd.ExecuteReader();if(!row.Read())throw new InvalidOperationException("Usuário não encontrado.");return new(user,row.GetString(0),row.IsDBNull(1)?null:row.GetString(1));}
    public async Task<DirectCallInfo> Start(long caller,long callee,string connection)
    {
        if(!friends.Related(caller,callee,true))throw new InvalidOperationException("Vocês precisam ser amigos para ligar.");
        if(rooms.UserBusy(caller)||rooms.UserBusy(callee))throw new InvalidOperationException("Saia da chamada atual antes de começar outra.");
        var info=registry.Begin(Profile(caller),Profile(callee),connection);await hub.Clients.Group($"user:{callee}").SendAsync("IncomingDirectCall",info);await activity.RoomChanged(info.Id,info);return info;
    }
    public async Task Accept(long id,long user,string connection)
    {
        var before=registry.Get(id)??throw new InvalidOperationException("Chamada encerrada.");
        if(!friends.Related(before.Caller.Id,before.Callee.Id,true))throw new InvalidOperationException("A amizade não está mais ativa.");
        var info=registry.Accept(id,user,connection);
        await hub.Clients.Groups(new[]{$"user:{info.Caller.Id}",$"user:{info.Callee.Id}"}).SendAsync("DirectCallReady",info);await activity.RoomChanged(info.Id,info);
    }
    public async Task End(long id,string reason)
    {
        var info=registry.End(id,reason);if(info==null)return;
        foreach(var (connection,channel) in rooms.CloseChannels([id])){await hub.Groups.RemoveFromGroupAsync(connection,$"call:{channel}");await hub.Clients.Client(connection).SendAsync("CallEnded",channel);}
        await hub.Clients.Groups(new[]{$"user:{info.Caller.Id}",$"user:{info.Callee.Id}"}).SendAsync("DirectCallEnded",info);await activity.RoomChanged(info.Id,info);await activity.PresenceChanged(info.Caller.Id);await activity.PresenceChanged(info.Callee.Id);
    }
    public async Task Disconnect(string connection,long? user)
    {
        foreach(var id in registry.ForConnection(connection))await End(id,"A conexão com a outra pessoa foi encerrada.");
        if(user!=null&&!presence.Online(user.Value))foreach(var id in registry.ForUser(user.Value))await End(id,"A outra pessoa ficou offline.");
    }
}
public sealed class DirectCallTimeout(DirectCallRegistry registry,DirectCallManager manager):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop){try{using var timer=new PeriodicTimer(TimeSpan.FromSeconds(3));while(await timer.WaitForNextTickAsync(stop))foreach(var id in registry.Expired())await manager.End(id,"A chamada não foi atendida.");}catch(OperationCanceledException){}}
}
