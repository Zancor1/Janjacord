using JanjaCord.Server.Database;
using JanjaCord.Shared;
using Microsoft.AspNetCore.SignalR;
namespace JanjaCord.Server.Networking;
public static class DirectGroupEndpoints
{
    public static void MapDirectGroups(this WebApplication app)
    {
        app.MapGet("/api/groups",(HttpContext ctx,DirectGroupStore groups)=>groups.List((long)ctx.Items["user"]!));
        app.MapPost("/api/groups",async(CreateDirectGroup input,HttpContext ctx,DirectGroupStore groups,IHubContext<ChatHub> hub)=>{
            try{var group=groups.Create((long)ctx.Items["user"]!,input);await hub.Clients.Groups(group.Members.Select(m=>$"user:{m.Id}").ToArray()).SendAsync("DirectGroupsChanged");return Results.Ok(group);}catch(InvalidOperationException ex){return Results.BadRequest(ex.Message);}
        });
        app.MapGet("/api/groups/{id:long}/messages",(long id,long? before,HttpContext ctx,DirectGroupStore groups)=>{
            try{return Results.Ok(groups.History((long)ctx.Items["user"]!,id,before));}catch(InvalidOperationException){return Results.StatusCode(403);}
        });
    }
}
