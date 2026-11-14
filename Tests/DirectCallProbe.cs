using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using JanjaCord.Shared;
public static class DirectCallProbe
{
    public static async Task Run(HubConnection a,HubConnection b,Session sa,Session sb)
    {
        static TaskCompletionSource<T> Next<T>()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Denied(Func<Task> action){try{await action();throw new Exception("Operação indevida permitida na chamada privada.");}catch(HubException){}}
        var incoming=Next<DirectCallInfo>();var readyA=Next<DirectCallInfo>();var readyB=Next<DirectCallInfo>();var endedA=Next<DirectCallInfo>();var endedB=Next<DirectCallInfo>();
        using var s1=b.On<DirectCallInfo>("IncomingDirectCall",x=>incoming.TrySetResult(x));using var s2=a.On<DirectCallInfo>("DirectCallReady",x=>readyA.TrySetResult(x));using var s3=b.On<DirectCallInfo>("DirectCallReady",x=>readyB.TrySetResult(x));using var s4=a.On<DirectCallInfo>("DirectCallEnded",x=>endedA.TrySetResult(x));using var s5=b.On<DirectCallInfo>("DirectCallEnded",x=>endedB.TrySetResult(x));
        var call=await a.InvokeAsync<DirectCallInfo>("StartDirectCall",sb.UserId);
        if((await incoming.Task.WaitAsync(TimeSpan.FromSeconds(5))).Id!=call.Id||call.Id>=0)throw new Exception("Convite não chegou.");
        await Denied(()=>a.InvokeAsync("JoinCall",call.Id));await Denied(()=>a.InvokeAsync("AcceptDirectCall",call.Id));await Denied(()=>b.InvokeAsync<DirectCallInfo>("StartDirectCall",sa.UserId));
        await b.InvokeAsync("EndDirectCall",call.Id);await Task.WhenAll(endedA.Task,endedB.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Console.WriteLine("PASS: convite/recusa de DM, bloqueio de chamada ocupada e consentimento antes de liberar mídia");
        incoming=Next<DirectCallInfo>();endedA=Next<DirectCallInfo>();endedB=Next<DirectCallInfo>();
        call=await a.InvokeAsync<DirectCallInfo>("StartDirectCall",sb.UserId);await incoming.Task.WaitAsync(TimeSpan.FromSeconds(5));await b.InvokeAsync("AcceptDirectCall",call.Id);
        await Task.WhenAll(readyA.Task,readyB.Task).WaitAsync(TimeSpan.FromSeconds(5));
        var first=await a.InvokeAsync<CallRoster>("JoinCall",call.Id);var both=await b.InvokeAsync<CallRoster>("JoinCall",call.Id);if(first.Peers.Length!=1||both.Peers.Length!=2)throw new Exception("Sala privada incorreta.");
        var signal=Next<RtcSignal>();using var s6=b.On<RtcSignal>("RtcSignal",x=>signal.TrySetResult(x));await a.InvokeAsync("Signal",new SendRtcSignal(b.ConnectionId!,"audio-ice","test-transport"));if((await signal.Task.WaitAsync(TimeSpan.FromSeconds(5))).From!=a.ConnectionId)throw new Exception("Sinalização privada incorreta.");
        await a.InvokeAsync("LeaveCall");await Task.WhenAll(endedA.Task,endedB.Task).WaitAsync(TimeSpan.FromSeconds(5));await Denied(()=>b.InvokeAsync("JoinCall",call.Id));
        Console.WriteLine("PASS: atender DM cria sala privada, sinaliza áudio e desligar encerra nos dois lados");
    }
}
