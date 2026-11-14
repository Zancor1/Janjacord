using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using JanjaCord.Client.Media;
using JanjaCord.Shared;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    DirectCallInfo? directCall;
    readonly IncomingRingtone ringtone=new();
    Window? incomingDialog;long? incomingId;bool incomingHandled;
    readonly HashSet<long> finishedCalls=[];
    Button? directCallButton,compactMic,compactCamera,compactShare,compactEnd,retryVoice;
    Border? callSurface;
    readonly WrapPanel callPeople=new(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
    readonly HashSet<string> activeScreens=[];
    readonly TextBlock compactStatus=Ui.Label("",12,Ui.Muted);
    void ConnectDirectCalls()
    {
        connection.IncomingDirectCall+=info=>Dispatcher.UIThread.Post(()=>_ = Safe(()=>ShowIncoming(info)));
        connection.DeliverPendingCalls();
        connection.DirectCallReady+=info=>Dispatcher.UIThread.Post(()=>_ = Safe(()=>DirectReady(info)));
        connection.DirectCallEnded+=info=>Dispatcher.UIThread.Post(()=>_ = Safe(()=>DirectEnded(info)));
        connection.Interrupted+=()=>Dispatcher.UIThread.Post(()=>{if(incomingId is long id)CloseIncoming(id);ringtone.Stop();directCall=null;groupVoice=null;ClearCallActivities();});
    }
    async Task StartDirect()
    {
        if(activeGroup is {} group){await JoinGroupVoice(group,VisibleCall()==null);return;}
        if(activeDirect==null)return;if(call!.InCall||directCall!=null)throw new Exception("Encerre a chamada atual antes de ligar.");
        var info=await connection.StartDirectCall(activeDirect.Id);
        if(finishedCalls.Contains(info.Id))return;
        if(directCall?.Id!=info.Id)directCall=info;
        UpdateCompactCall();
    }
    async Task ShowIncoming(DirectCallInfo info)
    {
        if(finishedCalls.Contains(info.Id)||incomingDialog!=null)return;
        RestoreFromTray();incomingId=info.Id;incomingHandled=false;
        var dialog=new Window{Title="Chamada recebida",Width=420,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};incomingDialog=dialog;Ui.ApplyStyles(dialog);
        void Ring(){try{ringtone.Start(profile.OutputDeviceId);}catch(Exception ex){Report("Não foi possível tocar o toque de chamada: "+ex.Message);}}
        async Task Respond(Func<Task> action){ringtone.Stop();try{await action();}catch{if(incomingId==info.Id&&!finishedCalls.Contains(info.Id)&&connection.Connected)Ring();throw;}}
        var accept=Button("Atender",()=>Respond(()=>connection.AcceptDirectCall(info.Id)),true);accept.Name="AcceptDirectCall";
        var reject=Button("Recusar",()=>Respond(()=>connection.EndDirectCall(info.Id)));reject.Name="RejectDirectCall";reject.Background=Ui.Brush("#B72C40");reject.Foreground=Brushes.White;
        dialog.Content=new Border{Padding=new Thickness(28),Child=Ui.Stack(18,new Border{Child=Ui.Avatar(info.Caller.Avatar,info.Caller.Name,84),HorizontalAlignment=HorizontalAlignment.Center},Ui.Label(info.Caller.Name,25),Ui.Label("Está ligando para você…",14,Ui.Muted),Ui.Columns("*,*",reject,accept))};
        dialog.Opened+=(_,_)=>Ring();
        try{await dialog.ShowDialog(this);}finally{ringtone.Stop();}
        if(incomingId==info.Id){var refuse=!incomingHandled;incomingDialog=null;incomingId=null;if(refuse&&connection.Connected)await connection.EndDirectCall(info.Id);}
    }
    void CloseIncoming(long id){if(incomingId!=id)return;ringtone.Stop();incomingHandled=true;incomingDialog?.Close();incomingDialog=null;incomingId=null;}
    async Task DirectReady(DirectCallInfo info)
    {
        CloseIncoming(info.Id);if(finishedCalls.Contains(info.Id))return;
        var own=connection.ConnectionId;if(own!=info.CallerConnection&&own!=info.CalleeConnection)return;
        directCall=info;var other=info.Caller.Id==connection.Session!.UserId?info.Callee:info.Caller;
        try{if(call!.RoomId!=info.Id)await call.Join(info.Id);if(finishedCalls.Contains(info.Id))return;
            var friend=friends.FirstOrDefault(f=>f.Id==other.Id);if(friend!=null&&activeDirect?.Id!=other.Id)await OpenDirect(friend);UpdateCompactCall();}
        catch{await connection.EndDirectCall(info.Id);throw;}
    }
    async Task DirectEnded(DirectCallInfo info)
    {
        finishedCalls.Add(info.Id);CloseIncoming(info.Id);
        if(directCall?.Id!=info.Id)return;directCall=null;
        if(call?.RoomId==info.Id)await call.Leave();UpdateCompactCall();status.Text=info.Reason??"Chamada encerrada.";
    }
    async Task EndCurrentCall(){if(directCall is {} info)await connection.EndDirectCall(info.Id);await call!.Leave();directCall=null;voiceChannel=null;groupVoice=null;UpdateCompactCall();}
    Control BuildCompactCall()
    {
        Button Control(string icon,string label,Func<Task> action,string id){var b=IconButton(icon,label,action);b.Name=id;b.Width=48;b.Height=48;b.CornerRadius=new CornerRadius(12);b.Content=Ui.Icon(icon,21,Ui.Text);return b;}
        compactMic=Control("mic","Silenciar microfone",()=>call!.ToggleMute(),"CallMicrophone");
        AddMicrophoneSettings(compactMic);
        compactCamera=Control("camera","Ligar câmera",ToggleCamera,"CallCamera");
        compactShare=Control("screen","Compartilhar tela",ToggleShare,"CallScreenShare");
        var more=Control("more","Microfone e dispositivo de áudio",AudioSettings,"CallAudioDevices");
        compactEnd=Control("leave","Encerrar chamada",EndCurrentCall,"CallHangup");compactEnd.Width=64;compactEnd.Background=Ui.Brush("#DA293F");
        var pill=new Border{Child=Ui.Columns("Auto,Auto,Auto,Auto",compactMic,compactCamera,compactShare,more),CornerRadius=new CornerRadius(15),BorderThickness=new Thickness(1),BorderBrush=Ui.Line,Background=Ui.Brush("#0D0D10"),Padding=new Thickness(4,0)};
        compactControls=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12,HorizontalAlignment=HorizontalAlignment.Center,Children={pill,compactEnd}};
        joinConversationButton=Button("Entrar na chamada",JoinVisibleCall,true);joinConversationButton.Name="JoinConversationCall";joinConversationButton.Background=Ui.Green;joinConversationButton.HorizontalAlignment=HorizontalAlignment.Center;
        compactStatus.Name="ConversationCallStatus";
        compactStatus.HorizontalAlignment=HorizontalAlignment.Center;
        retryVoice=Button("Reconectar voz",()=>call!.RetryAudio());retryVoice.Name="RetryCallAudio";retryVoice.HorizontalAlignment=HorizontalAlignment.Center;retryVoice.IsVisible=false;
        callSurface=new Border{Name="CallSurface",Child=Ui.Stack(16,new ScrollViewer{Content=callPeople,MaxHeight=240},compactStatus,retryVoice,joinConversationButton,compactControls),Padding=new Thickness(18,24),Background=Ui.Brush("#080809"),IsVisible=false};return callSurface;
    }
    void UpdateCompactCall()
    {
        if(callSurface==null||call==null)return;
        var activity=VisibleCall();
        var localDirect=directCall!=null&&activeDirect!=null&&(directCall.Caller.Id==activeDirect.Id||directCall.Callee.Id==activeDirect.Id);
        var groupVisible=activeGroup!=null&&(activity!=null||call.RoomId==GroupRoomBase-activeGroup.Id);
        var directVisible=localDirect||activity?.Direct!=null;
        var busyOther=activeDirect?.InCall==true&&activity==null&&!localDirect;
        var guildLive=activeGroup==null&&activeDirect==null&&call.InCall&&call.RoomId>=0;
        callSurface.IsVisible=directVisible||groupVisible||busyOther||guildLive;
        long? displayedRoom=activity?.RoomId??(groupVisible?GroupRoomBase-activeGroup!.Id:localDirect?directCall!.Id:guildLive?call.RoomId:null);
        var live=call.InCall&&call.RoomId==displayedRoom;
        var ownPending=localDirect&&!live;
        compactControls!.IsVisible=live||ownPending;
        joinConversationButton!.IsVisible=!live&&activity!=null&&(activeGroup!=null||activity.Direct is {State:"ringing"} d&&d.Callee.Id==connection.Session?.UserId);
        joinConversationButton.IsEnabled=connection.Connected&&!call.InCall&&directCall==null;
        compactMic!.IsEnabled=compactCamera!.IsEnabled=compactShare!.IsEnabled=live;compactEnd!.IsEnabled=live||ownPending;
        compactMic.Content=Ui.Icon(call.Muted?"mic-off":"mic",21,call.Muted?Ui.Red:Ui.Text);compactCamera.Content=Ui.Icon("camera",21,call.CameraOn?Ui.Green:Ui.Text);compactShare.Content=Ui.Icon("screen",21,call.Sharing?Ui.Green:Ui.Text);
        ToolTip.SetTip(compactMic,call.Muted?"Ativar microfone":"Silenciar microfone");ToolTip.SetTip(compactCamera,call.CameraOn?"Desligar câmera":"Ligar câmera");ToolTip.SetTip(compactShare,call.Sharing?"Parar transmissão":"Compartilhar tela");
        compactStatus.Text=busyOther?$"{activeDirect!.Name} está em outra chamada":!live&&activity!=null?(activity.State=="ringing"?"Chamada em andamento · aguardando atender":$"Chamada em andamento · {activity.Participants.Length} na voz"):localDirect&&directCall!.State=="ringing"?"Chamando…":!call.HasOtherParticipants?"Aguardando outra pessoa entrar":call.AudioFailed?"Não foi possível conectar a voz. Tente reconectar.":call.AudioConnected?"Voz conectada · câmera e tela são opcionais":"Conectando áudio…";
        retryVoice!.IsVisible=live&&call.AudioFailed;
        callPeople.Children.Clear();
        IEnumerable<MemberProfile> people=live&&currentRoster.ChannelId==displayedRoom?currentRoster.Peers.Select(p=>new MemberProfile(p.UserId,p.Name,p.Avatar)):activity!=null?activity.Participants:busyOther?[new(activeDirect!.Id,activeDirect.Name,activeDirect.Avatar)]:localDirect?[directCall!.Caller]:[];
        foreach(var p in people){
            var name=Ui.Label(p.Name,13);name.Name="VoiceParticipant_"+p.Id;name.HorizontalAlignment=HorizontalAlignment.Center;
            var person=Ui.Stack(9,CallAvatar(p,72),name);person.HorizontalAlignment=HorizontalAlignment.Center;
            callPeople.Children.Add(new Border{Width=174,MinHeight=128,Padding=new Thickness(16),Margin=new Thickness(4),CornerRadius=new CornerRadius(12),Background=Ui.Brush(p.Id%2==0?"#292632":"#24312E"),Child=person});
        }
        if(live)foreach(var id in activeScreens){var peer=currentRoster.Peers.FirstOrDefault(p=>p.ConnectionId==id);if(peer==null)continue;
            if(videoTiles.TryGetValue(id+":screen",out var tile)){DetachVideo(tile);tile.SetAutomationId("ScreenParticipant_"+peer.UserId);callPeople.Children.Add(tile.Root);}
            else {var label=Ui.Label("Tela de "+peer.Name,12);label.Name="ScreenParticipant_"+peer.UserId;callPeople.Children.Add(new Border{Margin=new Thickness(12,8),Padding=new Thickness(16),Background=Ui.Panel,CornerRadius=new CornerRadius(12),Child=Ui.Stack(9,Ui.Icon("screen",28,Ui.Green),label,Ui.Label("Recebendo transmissão…",11,Ui.Muted))});}
        }
        if(directCallButton!=null){directCallButton.IsVisible=activeDirect?.State=="accepted"||activeGroup!=null;directCallButton.IsEnabled=connection.Connected&&!call.InCall&&directCall==null&&!busyOther;ToolTip.SetTip(directCallButton,activity!=null?"Entrar na chamada":"Iniciar chamada");}
    }
}
