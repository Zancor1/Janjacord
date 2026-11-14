using Avalonia.Controls;
using JanjaCord.Shared;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    readonly Dictionary<long,CallActivity> conversationCalls=[];
    readonly Dictionary<long,long> activityUpdates=[],presenceUpdates=[];
    readonly HashSet<long> busyUsers=[];
    readonly Dictionary<long,long> activityRevisions=[],presenceRevisions=[];
    long activitySequence,presenceSequence,activitySnapshotFloor,presenceSnapshotFloor;
    Button? joinConversationButton;
    StackPanel? compactControls;
    CallActivity? VisibleCall()
    {
        if(activeGroup!=null)return conversationCalls.GetValueOrDefault(GroupRoomBase-activeGroup.Id);
        if(activeDirect!=null){var own=connection.Session?.UserId;return conversationCalls.Values.FirstOrDefault(a=>a.Direct is {} d&&((d.Caller.Id==own&&d.Callee.Id==activeDirect.Id)||(d.Callee.Id==own&&d.Caller.Id==activeDirect.Id)));}
        return null;
    }
    void ReceiveCallActivity(CallActivity activity)
    {
        if(activity.Revision<=activitySnapshotFloor||activityRevisions.GetValueOrDefault(activity.RoomId)>=activity.Revision)return;activityRevisions[activity.RoomId]=activity.Revision;
        activityUpdates[activity.RoomId]=++activitySequence;
        if(activity.State=="ended")conversationCalls.Remove(activity.RoomId);else conversationCalls[activity.RoomId]=activity;
        RenderCallPresence();
    }
    void ReceiveCallPresence(CallPresence presence)
    {
        if(presence.Revision<=presenceSnapshotFloor||presenceRevisions.GetValueOrDefault(presence.UserId)>=presence.Revision)return;presenceRevisions[presence.UserId]=presence.Revision;
        presenceUpdates[presence.UserId]=++presenceSequence;
        if(presence.InCall)busyUsers.Add(presence.UserId);else busyUsers.Remove(presence.UserId);
        RenderCallPresence();
    }
    async Task RefreshCallActivity()
    {
        if(!connection.Connected)return;
        var connectionId=connection.ConnectionId;var activityVersion=activitySequence;var presenceVersion=presenceSequence;
        var snapshot=await connection.Get<CallActivitySnapshot>("/api/calls/activity");
        if(!connection.Connected||connectionId!=connection.ConnectionId)return;
        var rooms=snapshot.Calls.Select(c=>c.RoomId).ToHashSet();
        foreach(var id in conversationCalls.Keys.ToArray())if(!rooms.Contains(id)&&activityUpdates.GetValueOrDefault(id)<=activityVersion&&activityRevisions.GetValueOrDefault(id)<=snapshot.Revision){conversationCalls.Remove(id);activityRevisions[id]=snapshot.Revision;}
        foreach(var activity in snapshot.Calls)if(activityUpdates.GetValueOrDefault(activity.RoomId)<=activityVersion&&activityRevisions.GetValueOrDefault(activity.RoomId)<=activity.Revision){conversationCalls[activity.RoomId]=activity;activityRevisions[activity.RoomId]=activity.Revision;}
        var busy=snapshot.BusyUsers.ToHashSet();
        foreach(var id in busyUsers.ToArray())if(!busy.Contains(id)&&presenceUpdates.GetValueOrDefault(id)<=presenceVersion&&presenceRevisions.GetValueOrDefault(id)<=snapshot.Revision){busyUsers.Remove(id);presenceRevisions[id]=snapshot.Revision;}
        foreach(var id in busy)if(presenceUpdates.GetValueOrDefault(id)<=presenceVersion&&presenceRevisions.GetValueOrDefault(id)<=snapshot.Revision){busyUsers.Add(id);presenceRevisions[id]=snapshot.Revision;}
        activitySnapshotFloor=Math.Max(activitySnapshotFloor,snapshot.Revision);presenceSnapshotFloor=Math.Max(presenceSnapshotFloor,snapshot.Revision);
        RenderCallPresence();
    }
    void RenderCallPresence()
    {
        for(var i=0;i<friends.Count;i++)friends[i]=friends[i] with{InCall=busyUsers.Contains(friends[i].Id)};
        if(activeDirect!=null){activeDirect=activeDirect with{InCall=busyUsers.Contains(activeDirect.Id)};SetDirectState();}
        if(activeGroup!=null)RenderGroupInfo();
        RenderFriends();UpdateCompactCall();
    }
    void ClearCallActivities(){conversationCalls.Clear();busyUsers.Clear();activityRevisions.Clear();presenceRevisions.Clear();activitySnapshotFloor=presenceSnapshotFloor=0;activitySequence++;presenceSequence++;RenderCallPresence();}
    async Task JoinVisibleCall()
    {
        var activity=VisibleCall();if(activity==null)return;
        if(call!.InCall)throw new Exception("Saia da chamada atual antes de entrar nesta.");
        if(activeGroup is {} group){await JoinGroupVoice(group,false);return;}
        if(activity.Direct is {} info&&info.State=="ringing"&&info.Callee.Id==connection.Session?.UserId){await connection.AcceptDirectCall(info.Id);return;}
        throw new Exception("Esta chamada não está disponível para entrar nesta conexão.");
    }
}
