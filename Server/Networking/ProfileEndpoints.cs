using JanjaCord.Server.Authentication;
using JanjaCord.Server.Database;
using JanjaCord.Shared;
using Microsoft.AspNetCore.SignalR;
namespace JanjaCord.Server.Networking;
public static class ProfileEndpoints
{
    public static void MapProfiles(this WebApplication app)
    {
        app.MapPut("/api/profile",async(UpdateProfile input,HttpContext ctx,Store store,CallDirectory calls,IHubContext<ChatHub> hub,CallActivityPublisher activity)=>{
            if(ProfileValidation.Error(input.Name,input.Avatar) is string error)return Results.BadRequest(error);
            var user=(long)ctx.Items["user"]!;var name=NameRules.Normalize(input.Name);
            using var db=store.Open();using var transaction=db.BeginTransaction();
            using(var check=Store.Command(db,"SELECT 1 FROM user_names WHERE nameKey=$n AND userId<>$u",("$n",NameRules.Key(name)),("$u",user))){check.Transaction=transaction;if(check.ExecuteScalar()!=null)return Results.Conflict("Esse nome já está em uso nesta rede. Escolha outro nome.");}
            using(var cmd=Store.Command(db,"UPDATE users SET name=$n WHERE id=$u; INSERT INTO user_names(userId,nameKey) VALUES($u,$k) ON CONFLICT(userId) DO UPDATE SET nameKey=$k; INSERT INTO profiles(userId,avatar) VALUES($u,$a) ON CONFLICT(userId) DO UPDATE SET avatar=$a",("$u",user),("$n",name),("$k",NameRules.Key(name)),("$a",input.Avatar))){cmd.Transaction=transaction;cmd.ExecuteNonQuery();}
            var recipients=new HashSet<long>{user};
            using(var cmd=Store.Command(db,"SELECT CASE WHEN lowId=$u THEN highId ELSE lowId END FROM friendships WHERE lowId=$u OR highId=$u UNION SELECT m.userId FROM members m JOIN members own ON own.communityId=m.communityId WHERE own.userId=$u UNION SELECT m.userId FROM dm_group_members m JOIN dm_group_members own ON own.groupId=m.groupId WHERE own.userId=$u",("$u",user))){cmd.Transaction=transaction;using var rows=cmd.ExecuteReader();while(rows.Read())recipients.Add(rows.GetInt64(0));}
            transaction.Commit();var profile=new MemberProfile(user,name,input.Avatar);
            var rosters=calls.UpdateProfile(profile);
            await hub.Clients.Groups(recipients.Select(id=>$"user:{id}").ToArray()).SendAsync("ProfileChanged",profile);
            foreach(var roster in rosters){await hub.Clients.Group($"call:{roster.ChannelId}").SendAsync("CallRoster",roster);await activity.RoomChanged(roster.ChannelId); }
            return Results.Ok(profile);
        });
    }
}
