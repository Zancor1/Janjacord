using System.Security.Cryptography;
using System.Threading.RateLimiting;
using JanjaCord.Server.Authentication;
using JanjaCord.Server.Database;
using JanjaCord.Server.Networking;
using JanjaCord.Shared;
using Microsoft.AspNetCore.RateLimiting;


namespace JanjaCord.Server;
public static class ServerHost
{
    public static WebApplication Create(string[] args)
    {
var builder=WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o=>o.SingleLine=true);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics",LogLevel.Warning);
builder.WebHost.ConfigureKestrel(k=>k.Limits.MaxRequestBodySize=262144);
var legacyData=Path.Combine(AppContext.BaseDirectory,"data");
var defaultData=Directory.Exists(legacyData)?legacyData:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JanjaCord","Server");
var data=Path.GetFullPath(builder.Configuration["data-dir"]??defaultData); Directory.CreateDirectory(data);
if(!args.Contains("--local-test")) {
    var certificate=ServerCertificate.Load(data);
    if(builder.Configuration.GetValue<bool>("discover")){
        var identityPath=Path.Combine(data,"host-id.txt");if(!File.Exists(identityPath))File.WriteAllText(identityPath,Guid.NewGuid().ToString("N"));
        builder.Services.AddSingleton(new DiscoveryIdentity(File.ReadAllText(identityPath).Trim(),certificate.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256)));builder.Services.AddHostedService<DiscoveryService>();
    }
    var port=builder.Configuration.GetValue<int?>("port")??5000;
    builder.WebHost.ConfigureKestrel(k=>k.ListenAnyIP(port,l=>l.UseHttps(certificate)));
    Console.WriteLine($"HTTPS porta {port}. Impressão SHA-256 (copie para os clientes):");
    Console.WriteLine(certificate.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256));
    foreach(var network in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        foreach(var ip in network.GetIPProperties().UnicastAddresses)
            if(ip.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork)Console.WriteLine($"  {network.Name}: https://{ip.Address}:{port}");
}
else builder.WebHost.UseUrls("http://127.0.0.1:5199");
builder.Services.AddSingleton(new Store(Path.Combine(data,"janjacord.db")));
builder.Services.AddSingleton<CallDirectory>();
builder.Services.AddSingleton<PresenceDirectory>();
builder.Services.AddSingleton<FriendStore>();
builder.Services.AddSingleton<DirectGroupStore>();
builder.Services.AddSingleton<DirectCallRegistry>();
builder.Services.AddSingleton<CallActivityPublisher>();
builder.Services.AddSingleton<DirectCallManager>();
builder.Services.AddHostedService<DirectCallTimeout>();
builder.Services.AddSignalR(o=>o.MaximumReceiveMessageSize=65536);
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("auth",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=30,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
var app=builder.Build();
app.UseRateLimiter();
app.Use(async(ctx,next)=>{
    if(ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.Path.StartsWithSegments("/api/auth")) {
        var header=ctx.Request.Headers.Authorization.ToString();
        var token=header.StartsWith("Bearer ")?header[7..]:"";
        var user=ctx.RequestServices.GetRequiredService<Store>().User(token);
        if(user==null){ctx.Response.StatusCode=401;return;} ctx.Items["user"]=user.Value;
    }
    await next();
});
app.MapGet("/health",()=>Results.Ok(new {application="JanjaCord",version="0.11.2",protocol=6}));
app.MapPost("/api/auth/device",DeviceAccounts.SignIn).RequireRateLimiting("auth");
app.MapPost("/api/auth/{action}",(string action,Credentials input,Store store)=>{
    if(action is not ("register" or "login")||string.IsNullOrWhiteSpace(input.Name)||input.Name.Trim().Length is <2 or >32||input.Password==null||input.Password.Length is <10 or >128) return Results.BadRequest("Nome: 2–32 caracteres. Senha: 10–128 caracteres.");
    using var db=store.Open();
    if(action=="register") {
        if(!NameRules.Valid(input.Name))return Results.BadRequest("Nome inválido.");input=input with{Name=NameRules.Normalize(input.Name)};
        using var tx=db.BeginTransaction();
        using var exists=Store.Command(db,"SELECT 1 FROM user_names WHERE nameKey=$n",("$n",NameRules.Key(input.Name)));exists.Transaction=tx;
        if(exists.ExecuteScalar()!=null)return Results.Conflict("Nome já cadastrado.");
        using var insert=Store.Command(db,"INSERT INTO users(name,hash) VALUES($n,$h); INSERT INTO user_names(userId,nameKey) VALUES(last_insert_rowid(),$k)",("$n",input.Name.Trim()),("$h",Passwords.Hash(input.Password)),("$k",NameRules.Key(input.Name)));insert.Transaction=tx;insert.ExecuteNonQuery();tx.Commit();
    }
    using var cmd=Store.Command(db,"SELECT id,name,hash FROM users WHERE name=$n",("$n",input.Name.Trim()));
    long id;string name;
    using(var row=cmd.ExecuteReader()) {if(!row.Read()||!Passwords.Verify(input.Password,row.GetString(2)))return Results.Unauthorized();id=row.GetInt64(0);name=row.GetString(1);}
    var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    using var session=Store.Command(db,"INSERT INTO sessions VALUES($t,$u,$e)",("$t",Store.TokenHash(token)),("$u",id),("$e",DateTimeOffset.UtcNow.AddDays(1).ToString("O")));session.ExecuteNonQuery();
    return Results.Ok(new Session(token,id,name));
}).RequireRateLimiting("auth");
app.MapGet("/api/communities",(HttpContext ctx,Store store)=>{
    using var db=store.Open();using var cmd=Store.Command(db,"SELECT c.id,c.name,c.ownerId FROM communities c JOIN members m ON m.communityId=c.id WHERE m.userId=$u",("$u",(long)ctx.Items["user"]!));using var rows=cmd.ExecuteReader();var result=new List<Community>();while(rows.Read())result.Add(new(rows.GetInt64(0),rows.GetString(1),rows.GetInt64(2)));return result;
});
app.MapPost("/api/communities",(CreateCommunity input,HttpContext ctx,Store store)=>{
    if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>60)return Results.BadRequest();
    using var db=store.Open();using var transaction=db.BeginTransaction();
    using var cmd=Store.Command(db,"UPDATE ids SET value=value+1 WHERE name='community'; INSERT INTO communities(id,name,ownerId) SELECT value,$n,$u FROM ids WHERE name='community'; SELECT last_insert_rowid();",("$n",input.Name.Trim()),("$u",(long)ctx.Items["user"]!));cmd.Transaction=transaction;var id=(long)cmd.ExecuteScalar()!;
    using var member=Store.Command(db,"INSERT INTO members VALUES($c,$u)",("$c",id),("$u",(long)ctx.Items["user"]!));member.Transaction=transaction;member.ExecuteNonQuery();transaction.Commit();return Results.Ok(new Community(id,input.Name.Trim(),(long)ctx.Items["user"]!));
});
app.MapPost("/api/communities/{id:long}/join",(long id,HttpContext ctx,Store store)=>{
    using var db=store.Open();using var exists=Store.Command(db,"SELECT 1 FROM communities WHERE id=$i",("$i",id));if(exists.ExecuteScalar()==null)return Results.NotFound("Esse servidor não existe. Confira o ID e tente novamente.");
    using var cmd=Store.Command(db,"INSERT OR IGNORE INTO members VALUES($c,$u)",("$c",id),("$u",(long)ctx.Items["user"]!));cmd.ExecuteNonQuery();return Results.Ok();
});
app.MapGet("/api/communities/{id:long}/channels",(long id,HttpContext ctx,Store store)=>{
    if(!store.Member((long)ctx.Items["user"]!,id))return Results.StatusCode(403);
    using var db=store.Open();using var cmd=Store.Command(db,"SELECT id,name,kind FROM channels WHERE communityId=$c",("$c",id));using var rows=cmd.ExecuteReader();var result=new List<Channel>();while(rows.Read())result.Add(new(rows.GetInt64(0),id,rows.GetString(1),rows.GetString(2)));return Results.Ok(result);
});
app.MapPost("/api/communities/{id:long}/channels",(long id,CreateChannel input,HttpContext ctx,Store store)=>{
    if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>60||input.Kind is not ("text" or "voice"))return Results.BadRequest();
    using var db=store.Open();using var owner=Store.Command(db,"SELECT 1 FROM communities WHERE id=$c AND ownerId=$u",("$c",id),("$u",(long)ctx.Items["user"]!));if(owner.ExecuteScalar()==null)return Results.StatusCode(403);
    using var transaction=db.BeginTransaction();using var cmd=Store.Command(db,"UPDATE ids SET value=value+1 WHERE name='channel'; INSERT INTO channels(id,communityId,name,kind) SELECT value,$c,$n,$k FROM ids WHERE name='channel'; SELECT last_insert_rowid();",("$c",id),("$n",input.Name.Trim()),("$k",input.Kind));cmd.Transaction=transaction;var channelId=(long)cmd.ExecuteScalar()!;transaction.Commit();return Results.Ok(new Channel(channelId,id,input.Name.Trim(),input.Kind));
});
app.MapGet("/api/channels/{id:long}/messages",(long id,long? before,HttpContext ctx,Store store)=>{
    if(!store.ChannelMember((long)ctx.Items["user"]!,id))return Results.StatusCode(403);
    using var db=store.Open();using var cmd=Store.Command(db,"SELECT m.id,m.userId,u.name,m.body,m.sentAt FROM messages m JOIN users u ON u.id=m.userId WHERE channelId=$c AND m.id<$b ORDER BY m.id DESC LIMIT 100",("$c",id),("$b",before??long.MaxValue));using var rows=cmd.ExecuteReader();var result=new List<ChatMessage>();while(rows.Read())result.Add(new(rows.GetInt64(0),id,rows.GetInt64(1),rows.GetString(2),rows.GetString(3),DateTimeOffset.Parse(rows.GetString(4))));result.Reverse();return Results.Ok(result);
});
app.MapHub<ChatHub>("/chat");
app.MapCommunityManagement();
app.MapFriends();
app.MapDirectGroups();
app.MapGet("/api/calls/activity",(HttpContext ctx,CallActivityPublisher activity)=>activity.Snapshot((long)ctx.Items["user"]!));
app.MapProfiles();
app.MapGet("/api/communities/{id:long}/members",(long id,HttpContext ctx,Store store)=>{
    if(!store.Member((long)ctx.Items["user"]!,id))return Results.StatusCode(403);
    using var db=store.Open();using var cmd=Store.Command(db,"SELECT u.id,u.name,p.avatar FROM users u JOIN members m ON m.userId=u.id LEFT JOIN profiles p ON p.userId=u.id WHERE m.communityId=$c ORDER BY u.name",("$c",id));using var rows=cmd.ExecuteReader();var result=new List<MemberProfile>();while(rows.Read())result.Add(new(rows.GetInt64(0),rows.GetString(1),rows.IsDBNull(2)?null:rows.GetString(2)));return Results.Ok(result);
});
Console.WriteLine("JanjaCord — Ctrl+C encerra. Dados persistidos em: "+data);
return app;

    }
}

