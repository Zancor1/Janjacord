using JanjaCord.Shared;
using JanjaCord.Server.Database;
using Microsoft.AspNetCore.SignalR;
namespace JanjaCord.Server.Networking;
// Observers receive member profiles, never SDP, ICE or connection identifiers from group rooms.
public sealed class CallActivityPublisher(CallDirectory rooms,DirectCallRegistry direct,DirectGroupStore groups,FriendStore friends,IHubContext<ChatHub> hub)
{
    readonly object gate=new();long revision;
    CallActivity Read(long room,DirectCallInfo? info=null)
    {
        lock(gate){
        var people=rooms.Snapshot(room).Peers.Select(p=>new MemberProfile(p.UserId,p.Name,p.Avatar)).ToArray();
        if(DirectGroupStore.IsRoom(room))return new(room,people.Length>0?"active":"ended",people,null,++revision);
        info=direct.Get(room)??(info==null?null:info with{State="ended"});
        if(info?.State=="ringing"&&people.Length==0)people=[info.Caller];
        return new(room,info?.State??"ended",people,info,++revision);
        }
    }
    public CallActivitySnapshot Snapshot(long user)
    {
        lock(gate){
        var mine=groups.List(user);var calls=mine.Select(g=>Read(DirectGroupStore.Room(g.Id))).Where(a=>a.State!="ended").ToList();
        foreach(var id in direct.ForUser(user)){var info=direct.Get(id);if(info!=null){var activity=Read(id,info);if(activity.State!="ended")calls.Add(activity);}}
        var visible=friends.List(user).Select(f=>f.Id).Concat(mine.SelectMany(g=>g.Members.Select(m=>m.Id))).Append(user).Distinct();
        return new(calls.ToArray(),visible.Where(rooms.UserBusy).ToArray(),++revision);
        }
    }
    public async Task RoomChanged(long room,DirectCallInfo? info=null)
    {
        var activity=Read(room,info);long[] audience;
        if(DirectGroupStore.IsRoom(room))audience=groups.Members(DirectGroupStore.RoomBase-room);
        else {var call=activity.Direct;if(call==null)return;audience=[call.Caller.Id,call.Callee.Id];}
        await hub.Clients.Groups(audience.Select(u=>$"user:{u}").ToArray()).SendAsync("CallActivityChanged",activity);
    }
    public Task PresenceChanged(long user)
    {
        CallPresence presence;lock(gate)presence=new(user,rooms.UserBusy(user),++revision);
        var audience=friends.List(user).Select(f=>f.Id).Concat(groups.List(user).SelectMany(g=>g.Members.Select(m=>m.Id))).Append(user).Distinct().Select(u=>$"user:{u}").ToArray();
        return hub.Clients.Groups(audience).SendAsync("CallPresenceChanged",presence);
    }
}
