using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JanjaCord.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
public static class GroupsProbe
{
    public static async Task Run(string address)
    {
        var clients=new List<HttpClient>();var sessions=new List<Session>();var suffix=Guid.NewGuid().ToString("N")[..8];
        static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS: "+text);}
        static TaskCompletionSource<T> Next<T>()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
        static async Task Denied(Func<Task> action){try{await action();throw new Exception("Unauthorized operation allowed");}catch(HubException){}}
        try{
            for(var i=0;i<12;i++){
                var client=new HttpClient{BaseAddress=new Uri(address)};clients.Add(client);using var result=await client.PostAsJsonAsync("/api/auth/register",new Credentials("group"+i+suffix,"Group-test-password123"));result.EnsureSuccessStatusCode();var session=(await result.Content.ReadFromJsonAsync<Session>())!;sessions.Add(session);client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.Token);
            }
            var owner=clients[0];
            for(var i=1;i<=10;i++){(await owner.PostAsJsonAsync("/api/friends",new FriendRequest(sessions[i].Name))).EnsureSuccessStatusCode();(await clients[i].PostAsJsonAsync($"/api/friends/{sessions[0].UserId}/accept",new{})).EnsureSuccessStatusCode();}
            var members=sessions.Skip(1).Take(9).Select(s=>s.UserId).ToArray();
            using var create=await owner.PostAsJsonAsync("/api/groups",new CreateDirectGroup("Turma de dez",members));create.EnsureSuccessStatusCode();var group=(await create.Content.ReadFromJsonAsync<DirectGroup>())!;
            Check(group.Members.Length==10&&group.Members.Any(m=>m.Id==sessions[0].UserId),"Group has exactly ten people including creator");
            Check((await owner.PostAsJsonAsync("/api/groups",new CreateDirectGroup("Too many",sessions.Skip(1).Take(10).Select(s=>s.UserId).ToArray()))).StatusCode==HttpStatusCode.BadRequest,"Server rejects eleven people");
            Check((await owner.PostAsJsonAsync("/api/groups",new CreateDirectGroup("Stranger",[sessions[11].UserId]))).StatusCode==HttpStatusCode.BadRequest,"Server rejects adding non-friend");
            Check((await clients[1].GetFromJsonAsync<DirectGroup[]>("/api/groups"))!.Any(g=>g.Id==group.Id),"Group visible to invited member");
            Check((await clients[11].GetAsync($"/api/groups/{group.Id}/messages")).StatusCode==HttpStatusCode.Forbidden,"Outsider cannot read group history");
            HubConnection Hub(int i)=>new HubConnectionBuilder().WithUrl(address+"/chat",o=>o.AccessTokenProvider=()=>Task.FromResult<string?>(sessions[i].Token)).Build();
            await using var a=Hub(0);await using var b=Hub(1);await using var outsider=Hub(11);await using var offline=Hub(2);
            var message=Next<GroupMessage>();var invitation=Next<GroupCallInvite>();var incoming=Next<DirectCallInfo>();var leaked=false;
            b.On<GroupMessage>("GroupMessage",m=>message.TrySetResult(m));b.On<GroupCallInvite>("GroupCallInvite",m=>invitation.TrySetResult(m));outsider.On<GroupMessage>("GroupMessage",_=>leaked=true);offline.On<DirectCallInfo>("IncomingDirectCall",m=>incoming.TrySetResult(m));
            await a.StartAsync();await b.StartAsync();await outsider.StartAsync();
            var ringing=await a.InvokeAsync<DirectCallInfo>("StartDirectCall",sessions[2].UserId);Check(ringing.State=="ringing","Calling offline friend starts ringing without error");
            await offline.StartAsync();Check((await incoming.Task.WaitAsync(TimeSpan.FromSeconds(5))).Id==ringing.Id,"Offline friend receives pending call after connecting");await offline.InvokeAsync("AcceptDirectCall",ringing.Id);await a.InvokeAsync("EndDirectCall",ringing.Id);
            var key=Guid.NewGuid().ToString();var sent=await a.InvokeAsync<GroupMessage>("SendGroup",new SendGroupMessage(group.Id,"Group message saved",key));Check((await message.Task.WaitAsync(TimeSpan.FromSeconds(5))).Id==sent.Id,"Group message delivered by SignalR");
            var duplicate=await a.InvokeAsync<GroupMessage>("SendGroup",new SendGroupMessage(group.Id,"Group message saved",key));Check(duplicate.Id==sent.Id,"Retry is idempotent");await Task.Delay(100);Check(!leaked,"Group message not broadcast to outsider");
            Check((await clients[2].GetFromJsonAsync<GroupMessage[]>($"/api/groups/{group.Id}/messages"))!.Single().Id==sent.Id,"Member recovers stored history");await Denied(()=>outsider.InvokeAsync("SendGroup",new SendGroupMessage(group.Id,"Intrusion",Guid.NewGuid().ToString())));
            var room=-1_000_000_000_000-group.Id;await CallActivityProbe.Run(a,b,outsider,clients[1],clients[11],sessions[0].UserId,sessions[1].UserId,room);await Denied(()=>outsider.InvokeAsync("JoinCall",room));await a.InvokeAsync("JoinCall",room);await a.InvokeAsync("StartGroupCall",group.Id);Check((await invitation.Task.WaitAsync(TimeSpan.FromSeconds(5))).Group.Id==group.Id,"Group call invitation reaches members");
            Check((await b.InvokeAsync<CallRoster>("JoinCall",room)).Peers.Length==2,"Members can join private group voice room");await a.InvokeAsync("LeaveCall");Check((await a.InvokeAsync<CallRoster>("JoinCall",room)).Peers.Length==2,"One member leaving does not end group call");
            await a.InvokeAsync("LeaveCall");await b.InvokeAsync("LeaveCall");
            var connected=new List<HubConnection>();try{for(var i=0;i<10;i++){var h=Hub(i);connected.Add(h);await h.StartAsync();var roster=await h.InvokeAsync<CallRoster>("JoinCall",room);Check(roster.Peers.Length==i+1,$"Group roster supports {i+1} members");}}finally{foreach(var h in connected)await h.DisposeAsync();}
            var state=new{Address=address,Token=sessions[0].Token,GroupId=group.Id,MessageId=sent.Id};File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"group-persistence.json"),System.Text.Json.JsonSerializer.Serialize(state));
        }finally{foreach(var c in clients)c.Dispose();}
    }
}
