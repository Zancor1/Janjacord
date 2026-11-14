using System.Net.Http.Json;
using JanjaCord.Shared;
using Microsoft.AspNetCore.SignalR.Client;
public static class CallActivityProbe
{
    public static async Task Run(HubConnection caller,HubConnection observer,HubConnection outsider,HttpClient member,HttpClient stranger,long owner,long peer,long room)
    {
        static TaskCompletionSource<CallActivity> Next()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
        var started=Next();var ended=Next();var presence=new TaskCompletionSource<CallPresence>(TaskCreationOptions.RunContinuationsAsynchronously);var leaked=false;
        using var first=observer.On<CallActivity>("CallActivityChanged",a=>{if(a.RoomId!=room)return;if(a.State=="ended")ended.TrySetResult(a);else started.TrySetResult(a);});
        using var second=outsider.On<CallActivity>("CallActivityChanged",a=>{if(a.RoomId==room)leaked=true;});
        using var third=observer.On<CallPresence>("CallPresenceChanged",p=>{if(p.UserId==owner&&p.InCall)presence.TrySetResult(p);});
        await caller.InvokeAsync("JoinCall",room);
        var activity=await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if(activity.Participants.Length!=1||activity.Participants[0].Id!=owner)throw new Exception("Observer did not receive actual call participants");
        await presence.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var snapshot=(await member.GetFromJsonAsync<CallActivitySnapshot>("/api/calls/activity"))!;
        if(snapshot.Calls.Single(c=>c.RoomId==room).Participants.Length!=1||!snapshot.BusyUsers.Contains(owner))throw new Exception("Late viewer cannot discover ongoing group call");
        if(!(await member.GetFromJsonAsync<FriendEntry[]>("/api/friends"))!.Single(f=>f.Id==owner).InCall)throw new Exception("Friend not marked as in call");
        if((await stranger.GetFromJsonAsync<CallActivitySnapshot>("/api/calls/activity"))!.Calls.Any(c=>c.RoomId==room)||leaked)throw new Exception("Private call activity leaked to outsider");
        Console.WriteLine("PASS: non-participant sees active group call and real participants; snapshot restores it; friend marked in call; outsider excluded");
        await caller.InvokeAsync("LeaveCall");await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if((await member.GetFromJsonAsync<CallActivitySnapshot>("/api/calls/activity"))!.Calls.Any(c=>c.RoomId==room)||(await member.GetFromJsonAsync<FriendEntry[]>("/api/friends"))!.Single(f=>f.Id==owner).InCall)throw new Exception("Last participant left but call still displayed");
        Console.WriteLine("PASS: last participant leaving clears the active call and busy status");
        var ring=await caller.InvokeAsync<DirectCallInfo>("StartDirectCall",peer);
        var direct=(await member.GetFromJsonAsync<CallActivitySnapshot>("/api/calls/activity"))!.Calls.Single(c=>c.RoomId==ring.Id);
        if(direct.State!="ringing"||direct.Participants.Length!=1||direct.Direct?.Caller.Id!=owner)throw new Exception("DM ringing state unavailable");
        await observer.InvokeAsync("AcceptDirectCall",ring.Id);await caller.InvokeAsync("JoinCall",ring.Id);await observer.InvokeAsync("JoinCall",ring.Id);
        if((await member.GetFromJsonAsync<CallActivitySnapshot>("/api/calls/activity"))!.Calls.Single(c=>c.RoomId==ring.Id).Participants.Length!=2)throw new Exception("DM participant cards missing");
        if((await stranger.GetFromJsonAsync<CallActivitySnapshot>("/api/calls/activity"))!.Calls.Any(c=>c.RoomId==ring.Id))throw new Exception("DM presence details leaked");
        await caller.InvokeAsync("LeaveCall");
        if((await member.GetFromJsonAsync<CallActivitySnapshot>("/api/calls/activity"))!.Calls.Any(c=>c.RoomId==ring.Id))throw new Exception("Ended DM call still displayed");
        Console.WriteLine("PASS: DM ringing, connected participants and end state are visible only to the two users");
    }
}
