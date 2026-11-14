using JanjaCord.Server.Database;
using JanjaCord.Shared;
using Microsoft.AspNetCore.SignalR;
namespace JanjaCord.Server.Networking;
public static class FriendEndpoints
{
    static long User(HttpContext ctx)=>(long)ctx.Items["user"]!;
    static Task Notify(IHubContext<ChatHub> hub,long a,long b)=>hub.Clients.Groups(new[]{$"user:{a}",$"user:{b}"}).SendAsync("FriendsChanged");
    public static void MapFriends(this WebApplication app)
    {
        app.MapGet("/api/friends",(HttpContext ctx,FriendStore friends)=>friends.List(User(ctx)));
        app.MapPost("/api/friends",async(FriendRequest input,HttpContext ctx,Store store,FriendStore friends,IHubContext<ChatHub> hub)=>{
            if(!NameRules.Valid(input.Name))return Results.BadRequest("Digite o nome exato do seu amigo.");
            var user=User(ctx);using var db=store.Open();using var tx=db.BeginTransaction();
            using var lookup=Store.Command(db,"SELECT userId FROM user_names WHERE nameKey=$n",("$n",NameRules.Key(input.Name)));lookup.Transaction=tx;
            if(lookup.ExecuteScalar() is not long peer)return Results.NotFound("Esse nome ainda não foi encontrado nesta rede. A pessoa deve abrir o cliente e entrar na mesma rede.");
            if(peer==user)return Results.BadRequest("Você não pode adicionar a si mesmo.");
            using(var cmd=Store.Command(db,"INSERT INTO friendships(lowId,highId,requestedBy,state,createdAt) VALUES($l,$h,$u,'pending',$t) ON CONFLICT(lowId,highId) DO UPDATE SET state=CASE WHEN friendships.requestedBy<>$u THEN 'accepted' ELSE friendships.state END",("$l",Math.Min(user,peer)),("$h",Math.Max(user,peer)),("$u",user),("$t",DateTimeOffset.UtcNow.ToString("O")))){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
            tx.Commit();await Notify(hub,user,peer);return Results.Ok(friends.List(user).Single(f=>f.Id==peer));
        });
        app.MapPost("/api/friends/{peer:long}/accept",async(long peer,HttpContext ctx,Store store,IHubContext<ChatHub> hub)=>{
            var user=User(ctx);using var db=store.Open();using var cmd=Store.Command(db,"UPDATE friendships SET state='accepted' WHERE lowId=$l AND highId=$h AND requestedBy=$p",("$l",Math.Min(user,peer)),("$h",Math.Max(user,peer)),("$p",peer));if(cmd.ExecuteNonQuery()==0)return Results.StatusCode(403);await Notify(hub,user,peer);return Results.NoContent();
        });
        app.MapDelete("/api/friends/{peer:long}",async(long peer,HttpContext ctx,Store store,IHubContext<ChatHub> hub)=>{
            var user=User(ctx);using var db=store.Open();using var cmd=Store.Command(db,"DELETE FROM friendships WHERE lowId=$l AND highId=$h",("$l",Math.Min(user,peer)),("$h",Math.Max(user,peer)));cmd.ExecuteNonQuery();await Notify(hub,user,peer);return Results.NoContent();
        });
        app.MapGet("/api/direct/{peer:long}/messages",(long peer,long? before,HttpContext ctx,FriendStore friends)=>{
            var user=User(ctx);return friends.Related(user,peer)?Results.Ok(friends.History(user,peer,before)):Results.StatusCode(403);
        });
    }
}
