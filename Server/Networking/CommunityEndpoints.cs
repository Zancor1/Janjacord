using JanjaCord.Server.Database;
using Microsoft.AspNetCore.SignalR;
namespace JanjaCord.Server.Networking;
public static class CommunityEndpoints
{
    public static void MapCommunityManagement(this WebApplication app)
    {
        app.MapDelete("/api/communities/{id:long}",async(long id,HttpContext ctx,Store store,CallDirectory calls,IHubContext<ChatHub> hub,CallActivityPublisher activity)=>{
            var user=(long)ctx.Items["user"]!;
            using var db=store.Open();using var transaction=db.BeginTransaction();
            using var owner=Store.Command(db,"SELECT ownerId FROM communities WHERE id=$c",("$c",id));owner.Transaction=transaction;
            if(owner.ExecuteScalar() is not long ownerId)return Results.NotFound();
            if(ownerId!=user)return Results.StatusCode(403);
            var users=new List<long>();var channels=new List<long>();
            using(var cmd=Store.Command(db,"SELECT userId FROM members WHERE communityId=$c",("$c",id))){cmd.Transaction=transaction;using var rows=cmd.ExecuteReader();while(rows.Read())users.Add(rows.GetInt64(0));}
            using(var cmd=Store.Command(db,"SELECT id FROM channels WHERE communityId=$c",("$c",id))){cmd.Transaction=transaction;using var rows=cmd.ExecuteReader();while(rows.Read())channels.Add(rows.GetInt64(0));}
            using(var cmd=Store.Command(db,"DELETE FROM messages WHERE channelId IN (SELECT id FROM channels WHERE communityId=$c); DELETE FROM channels WHERE communityId=$c; DELETE FROM members WHERE communityId=$c; DELETE FROM communities WHERE id=$c",("$c",id))){cmd.Transaction=transaction;cmd.ExecuteNonQuery();}
            transaction.Commit();
            var closedUsers=channels.SelectMany(c=>calls.Snapshot(c).Peers).Select(p=>p.UserId).Distinct().ToArray();
            foreach(var (connection,channel) in calls.CloseChannels(channels)){
                await hub.Groups.RemoveFromGroupAsync(connection,$"call:{channel}");
                await hub.Clients.Client(connection).SendAsync("CallEnded",channel);
            }
            foreach(var closed in closedUsers)await activity.PresenceChanged(closed);
            await hub.Clients.Groups(users.Select(u=>$"user:{u}").ToArray()).SendAsync("CommunityDeleted",id);
            return Results.NoContent();
        });
    }
}
