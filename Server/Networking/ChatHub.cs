using Microsoft.AspNetCore.SignalR;
using JanjaCord.Server.Database;
using JanjaCord.Shared;
namespace JanjaCord.Server.Networking;
public sealed class ChatHub(Store store,CallDirectory calls,FriendStore friends,DirectGroupStore groups,PresenceDirectory presence,DirectCallRegistry directCalls,DirectCallManager directManager,CallActivityPublisher activity,ILogger<ChatHub> logger):Hub
{
    long User {
        get {
            var request=Context.GetHttpContext()!.Request;
            var header=request.Headers.Authorization.ToString();
            var token=header.StartsWith("Bearer ")?header[7..]:request.Query["access_token"].ToString();
            return store.User(token)??throw new HubException("Sessão inválida.");
        }
    }
    public override async Task OnConnectedAsync() {var user=User;await Groups.AddToGroupAsync(Context.ConnectionId,$"user:{user}");presence.Add(Context.ConnectionId,user);await NotifyPresence(user);foreach(var id in directCalls.ForUser(user)){var pending=directCalls.Get(id);if(pending?.State=="ringing"&&pending.Callee.Id==user)await Clients.Caller.SendAsync("IncomingDirectCall",pending);} logger.LogInformation("Usuário {UserId} conectado ({ConnectionId})",user,Context.ConnectionId); await base.OnConnectedAsync(); }
    public override async Task OnDisconnectedAsync(Exception? exception) {await LeaveCall();var user=presence.Remove(Context.ConnectionId);await directManager.Disconnect(Context.ConnectionId,user);if(user!=null)await NotifyPresence(user.Value);logger.LogInformation("Conexão {ConnectionId} encerrada",Context.ConnectionId);await base.OnDisconnectedAsync(exception);}
    public async Task<DirectCallInfo> StartDirectCall(long peer){try{return await directManager.Start(User,peer,Context.ConnectionId);}catch(InvalidOperationException ex){throw new HubException(ex.Message);}}
    public async Task AcceptDirectCall(long id){try{await directManager.Accept(id,User,Context.ConnectionId);}catch(InvalidOperationException ex){throw new HubException(ex.Message);}}
    public async Task EndDirectCall(long id){var info=directCalls.Get(id);if(info==null)return;if(info.Caller.Id!=User&&info.Callee.Id!=User)throw new HubException("Sem acesso à chamada.");await directManager.End(id,info.State=="ringing"?"Chamada cancelada ou recusada.":"Chamada encerrada.");}
    Task NotifyPresence(long user){var groups=friends.List(user).Select(f=>$"user:{f.Id}").ToArray();return groups.Length==0?Task.CompletedTask:Clients.Groups(groups).SendAsync("FriendsChanged");}
    public async Task<DirectMessage> SendDirect(SendDirectMessage input)
    {
        DirectMessage message;try{message=friends.Send(User,input);}catch(InvalidOperationException ex){throw new HubException(ex.Message);}
        await Clients.Groups(new[]{$"user:{message.SenderId}",$"user:{message.RecipientId}"}).SendAsync("DirectMessage",message);return message;
    }
    public async Task<GroupMessage> SendGroup(SendGroupMessage input)
    {
        GroupMessage message;try{message=groups.Send(User,input);}catch(InvalidOperationException ex){throw new HubException(ex.Message);}
        await Clients.Groups(groups.Members(input.GroupId).Select(id=>$"user:{id}").ToArray()).SendAsync("GroupMessage",message);return message;
    }
    public async Task StartGroupCall(long group)
    {
        if(calls.Room(Context.ConnectionId)!=DirectGroupStore.Room(group)||!groups.Member(User,group))throw new HubException("Entre na chamada do grupo primeiro.");
        var info=groups.Get(User,group);await Clients.Groups(info.Members.Where(m=>m.Id!=User).Select(m=>$"user:{m.Id}").ToArray()).SendAsync("GroupCallInvite",new GroupCallInvite(info,info.Members.Single(m=>m.Id==User)));
    }
    public async Task<CallRoster> JoinCall(long channel)
    {
        var user=User;
        bool Allowed()=>DirectGroupStore.IsRoom(channel)?groups.Member(user,DirectGroupStore.RoomBase-channel):channel<0?directCalls.Allowed(channel,user,Context.ConnectionId):store.ChannelMember(user,channel);
        if((channel>=0||DirectGroupStore.IsRoom(channel))&&directCalls.ForUser(user).Length>0)throw new HubException("Encerre a chamada privada antes de entrar no canal.");
        if(!Allowed())throw new HubException("Sem acesso ao canal ou chamada.");
        using var db=store.Open();using var kind=Store.Command(db,"SELECT kind FROM channels WHERE id=$c",("$c",channel));if(channel>=0&&(string?)kind.ExecuteScalar()!="voice")throw new HubException("Escolha um canal de voz.");
        await LeaveCall();
        using var cmd=Store.Command(db,"SELECT u.name,p.avatar FROM users u LEFT JOIN profiles p ON p.userId=u.id WHERE u.id=$u",("$u",user));using var row=cmd.ExecuteReader();row.Read();
        CallRoster roster;
        try{roster=calls.Join(Context.ConnectionId,channel,new(Context.ConnectionId,user,row.GetString(0),row.IsDBNull(1)?null:row.GetString(1)),Allowed);}catch(InvalidOperationException ex){throw new HubException(ex.Message);}
        await Groups.AddToGroupAsync(Context.ConnectionId,$"call:{channel}");
        await Clients.Group($"call:{channel}").SendAsync("CallRoster",roster);await activity.RoomChanged(channel);await activity.PresenceChanged(user);return roster;
    }
    public async Task LeaveCall()
    {
        if(calls.Leave(Context.ConnectionId) is not long channel)return;
        if(channel<0&&!DirectGroupStore.IsRoom(channel)){await Groups.RemoveFromGroupAsync(Context.ConnectionId,$"call:{channel}");await directManager.End(channel,"Chamada encerrada.");return;}
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,$"call:{channel}");
        await Clients.Group($"call:{channel}").SendAsync("CallRoster",calls.Snapshot(channel));await activity.RoomChanged(channel);await activity.PresenceChanged(User);
    }
    public async Task Signal(SendRtcSignal signal)
    {
        var user=User;
        if(signal.To==null||signal.Payload==null||signal.Payload.Length>48000||signal.Kind is not ("offer" or "answer" or "ice" or "renegotiate" or "media" or "audio-offer" or "audio-answer" or "audio-ice" or "audio-reset")||calls.Room(Context.ConnectionId) is not long channel||(DirectGroupStore.IsRoom(channel)?!groups.Member(user,DirectGroupStore.RoomBase-channel):channel<0?!directCalls.Allowed(channel,user,Context.ConnectionId):!store.ChannelMember(user,channel)))throw new HubException("Sinalização recusada.");
        if(!calls.SameRoom(Context.ConnectionId,signal.To))return; // Peer may have left while ICE/SDP was in flight.
        await Clients.Client(signal.To).SendAsync("RtcSignal",new RtcSignal(Context.ConnectionId,signal.Kind,signal.Payload));
    }
    public async Task Watch(long channel)
    { if(!store.ChannelMember(User,channel)) throw new HubException("Sem acesso."); await Groups.AddToGroupAsync(Context.ConnectionId,$"channel:{channel}"); }
    public async Task Unwatch(long channel) => await Groups.RemoveFromGroupAsync(Context.ConnectionId,$"channel:{channel}");
    public async Task Send(SendMessage input)
    {
        var user=User;
        if(string.IsNullOrWhiteSpace(input.Text)||input.Text.Length>4000||!store.ChannelMember(user,input.ChannelId)) throw new HubException("Mensagem inválida ou acesso negado.");
        using var db=store.Open();
        using var cmd=Store.Command(db,"INSERT INTO messages(channelId,userId,body,sentAt) VALUES($c,$u,$b,$t); SELECT last_insert_rowid();",("$c",input.ChannelId),("$u",user),("$b",input.Text),("$t",DateTimeOffset.UtcNow.ToString("O")));
        var id=(long)cmd.ExecuteScalar()!;
        using var read=Store.Command(db,"SELECT u.name,m.sentAt FROM messages m JOIN users u ON u.id=m.userId WHERE m.id=$i",("$i",id));
        using var row=read.ExecuteReader(); row.Read();
        await Clients.Group($"channel:{input.ChannelId}").SendAsync("Message",new ChatMessage(id,input.ChannelId,user,row.GetString(0),input.Text,DateTimeOffset.Parse(row.GetString(1))));
    }
}
