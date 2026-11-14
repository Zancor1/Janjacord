using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using JanjaCord.Shared;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    readonly List<DirectGroup> directGroups=[];
    readonly Dictionary<long,int> groupUnread=[];
    DirectGroup? activeGroup;
    Button? createGroupButton;
    long? groupVoice;
    Window? groupRing;
    (long Group,string Text,string ClientId)? groupDraftAttempt;
    async Task RefreshGroups()
    {
        if(!connection.Connected)return;
        var items=await connection.Get<DirectGroup[]>("/api/groups");directGroups.Clear();directGroups.AddRange(items);
        if(activeGroup!=null){activeGroup=directGroups.FirstOrDefault(g=>g.Id==activeGroup.Id);if(activeGroup!=null){RenderGroupInfo();draft.IsEnabled=sendButton!.IsEnabled=true;await SyncGroupHistory(activeGroup,selectionVersion);}else ShowHome();}
        RenderFriends();
    }
    void RenderGroupLinks()
    {
        foreach(var group in directGroups){
            var button=Button("",()=>OpenGroup(group));button.Name="DirectGroup_"+group.Id;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(10,8);button.Margin=new Thickness(7,0);button.Background=activeGroup?.Id==group.Id?Ui.Card:Brushes.Transparent;
            var unread=groupUnread.GetValueOrDefault(group.Id);var activity=conversationCalls.GetValueOrDefault(GroupRoomBase-group.Id);
            button.Content=Ui.Columns("Auto,*,Auto",Ui.Icon("users",28),new Border{Margin=new Thickness(9,0,0,0),Child=Ui.Stack(2,Ui.Label(group.Name,13),Ui.Label(activity!=null?$"Em chamada · {activity.Participants.Length} na voz":$"Grupo · {group.Members.Length}/10 pessoas",10,activity!=null?Ui.Green:Ui.Muted))},Ui.Label(unread>0?unread.ToString():"",11,Ui.Accent));directList.Children.Add(button);
        }
    }
    async Task CreateGroup()
    {
        if(activeDirect?.State!="accepted")return;
        await RefreshFriends();var peer=activeDirect;if(peer==null)return;
        var dialog=new Window{Title="Criar grupo",Width=470,Height=580,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);
        var name=Ui.Field("Nome do grupo (opcional)");name.Name="DirectGroupName";name.MaxLength=80;
        var find=Ui.Field("Buscar amigos");find.Name="DirectGroupSearch";
        var count=Ui.Label("",12,Ui.Muted);var error=Ui.Label("",12,Ui.Red);var rows=new StackPanel{Spacing=8};
        var choices=new List<(FriendEntry Friend,CheckBox Box)>();
        var create=Ui.Action("Criar grupo",true);create.Name="ConfirmCreateGroup";
        foreach(var friend in friends.Where(f=>f.State=="accepted")){
            var box=new CheckBox{Content=Ui.Columns("Auto,*",Ui.Avatar(friend.Avatar,friend.Name,28),new Border{Margin=new Thickness(10,0),Child=Ui.Label(friend.Name,14)}),IsChecked=friend.Id==peer.Id,Name="GroupMember_"+friend.Id};
            choices.Add((friend,box));rows.Children.Add(box);
        }
        void Refresh(){var selected=choices.Count(c=>c.Box.IsChecked==true);count.Text=$"{selected+1}/10 pessoas · você também participa";create.IsEnabled=selected is >=1 and <=9;foreach(var c in choices)c.Box.IsEnabled=c.Box.IsChecked==true||selected<9;}
        foreach(var c in choices)c.Box.IsCheckedChanged+=(_,_)=>Refresh();Refresh();
        find.TextChanged+=(_,_)=>{foreach(var c in choices)c.Box.IsVisible=c.Friend.Name.Contains(find.Text??"",StringComparison.OrdinalIgnoreCase);};
        create.Click+=async(_,_)=>{create.IsEnabled=false;try{
            var ids=choices.Where(c=>c.Box.IsChecked==true).Select(c=>c.Friend.Id).ToArray();if(ids.Length is <1 or >9)throw new Exception("Selecione até9 amigos, além de você.");
            var group=await connection.PostResult<DirectGroup,CreateDirectGroup>("/api/groups",new(name.Text??"",ids));dialog.Close();await RefreshGroups();await OpenGroup(group);
        }catch(Exception ex){error.Text=ex.Message;Refresh();}};
        var layout=new Grid{RowDefinitions=new RowDefinitions("Auto,*,Auto")};layout.Children.Add(Ui.Stack(10,Ui.Label("Crie sua turma",25),name,find,count));var scroll=new ScrollViewer{Content=rows,Margin=new Thickness(0,14)};Grid.SetRow(scroll,1);layout.Children.Add(scroll);var footer=Ui.Stack(8,error,create);Grid.SetRow(footer,2);layout.Children.Add(footer);dialog.Content=new Border{Padding=new Thickness(24),Child=layout};await dialog.ShowDialog(this);
    }
    async Task OpenGroup(DirectGroup group)
    {
        communityList.SelectedItem=null;activeCommunityId=null;selected=null;activeDirect=null;activeGroup=group;var version=++selectionVersion;
        ReplacePane(1,homeSidebar);ReplacePane(2,guildCenter);MoveDock(homePane,2);status=guildStatus;inviteButton!.IsVisible=false;createGroupButton!.IsVisible=false;
        title.Text=group.Name;subtitle.Text=$"Grupo · {group.Members.Length}/10 pessoas";headerSymbol.Content=Ui.Icon("users",24);search.Text="";draft.Text="";draft.Watermark="Mensagem para "+group.Name;draft.IsEnabled=sendButton!.IsEnabled=connection.Connected;
        messages.Clear();messageIds.Clear();groupUnread.Remove(group.Id);RenderGroupInfo();RenderFriends();UpdateCompactCall();_ = Safe(RefreshCallActivity);
        if(!connection.Connected){status.Text="Conecte-se à rede para carregar o histórico do grupo.";return;}
        await SyncGroupHistory(group,version);
    }
    async Task SyncGroupHistory(DirectGroup group,int version)
    {
        var old=await connection.Get<GroupMessage[]>($"/api/groups/{group.Id}/messages");if(version!=selectionVersion||activeGroup?.Id!=group.Id)return;
        var received=messages.ToArray();messages.Clear();messageIds.Clear();foreach(var message in old)AppendGroup(message);foreach(var message in received)Append(message);
    }
    void RenderGroupInfo()
    {
        if(activeGroup is not {} group)return;
        var list=Ui.Stack(14,Ui.Icon("users",48),Ui.Label(group.Name,22),Ui.Label($"PARTICIPANTES — {group.Members.Length}/10",11,Ui.Muted));
        foreach(var person in group.Members)list.Children.Add(Ui.Columns("Auto,*",Ui.Avatar(person.Avatar,person.Name,32),new Border{Margin=new Thickness(10,0),Child=Ui.Stack(2,Ui.Label(person.Name,13),Ui.Label(busyUsers.Contains(person.Id)?"Em chamada":"",11,Ui.Green))}));
        ReplacePane(3,new Border{Padding=new Thickness(18,24),Child=new ScrollViewer{Content=list},Background=Ui.Sidebar});
    }
    void AppendGroup(GroupMessage message)=>Append(new(message.Id,0,message.SenderId,message.Author,message.Text,message.SentAt));
    void ReceiveGroup(GroupMessage message){if(activeGroup?.Id==message.GroupId)AppendGroup(message);else if(message.SenderId!=connection.Session?.UserId){groupUnread[message.GroupId]=groupUnread.GetValueOrDefault(message.GroupId)+1;RenderFriends();}}
    const long GroupRoomBase=-1_000_000_000_000;
    async Task JoinGroupVoice(DirectGroup group,bool ring)
    {
        if(call!.InCall||directCall!=null)throw new Exception("Encerre sua chamada atual primeiro.");
        await call.Join(GroupRoomBase-group.Id);groupVoice=group.Id;await OpenGroup(group);UpdateCompactCall();
        if(ring)await connection.StartGroupCall(group.Id);
    }
    async Task ShowGroupRing(GroupCallInvite invite)
    {
        if(call!.InCall||directCall!=null||incomingDialog!=null||groupRing!=null||invite.Caller.Id==connection.Session?.UserId)return;
        RestoreFromTray();
        var dialog=new Window{Title="Chamada do grupo",Width=430,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};groupRing=dialog;Ui.ApplyStyles(dialog);
        var accept=Ui.Action("Entrar",true);accept.Name="AcceptGroupCall";accept.Click+=(_,_)=>dialog.Close(true);
        var reject=Ui.Action("Agora não");reject.Name="RejectGroupCall";reject.Click+=(_,_)=>dialog.Close(false);
        dialog.Content=new Border{Padding=new Thickness(26),Child=Ui.Stack(16,Ui.Icon("users",48),Ui.Label(invite.Group.Name,24),Ui.Label(invite.Caller.Name+" está chamando o grupo",14),Ui.Columns("*,*",reject,accept))};
        var timeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(35)};timeout.Tick+=(_,_)=>dialog.Close(false);timeout.Start();
        try{ringtone.Start(profile.OutputDeviceId);if(await dialog.ShowDialog<bool>(this))await JoinGroupVoice(invite.Group,false);}finally{timeout.Stop();ringtone.Stop();groupRing=null;}
    }
}
