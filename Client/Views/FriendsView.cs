using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using JanjaCord.Shared;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    readonly List<FriendEntry> friends=[];
    FriendEntry? activeDirect;
    readonly StackPanel directList=new(){Spacing=3},friendRows=new(){Spacing=0};
    readonly TextBlock friendsCount=Ui.Label("AMIGOS — 0",11,Ui.Muted);
    readonly TextBox friendsSearch=Ui.Field("Buscar amigos");
    string friendFilter="all";
    readonly Dictionary<long,int> unread=[];
    void ReplacePane(int column,Control view)
    {
        if(shell==null)return;var old=shell.Children.FirstOrDefault(c=>Grid.GetColumn(c)==column);if(old==view)return;if(old!=null)shell.Children.Remove(old);
        if(view.Parent is Panel parent)parent.Children.Remove(view);Grid.SetColumn(view,column);shell.Children.Add(view);
    }
    void MoveDock(Grid target,int row){if(sharedDock.Parent is Panel parent)parent.Children.Remove(sharedDock);Grid.SetRow(sharedDock,row);target.Children.Add(sharedDock);}
    void ShowGuild()
    {
        activeGroup=null;activeDirect=null;if(createGroupButton!=null)createGroupButton.IsVisible=false;if(directCallButton!=null)directCallButton.IsVisible=false;UpdateCompactCall();ReplacePane(1,guildSidebar);ReplacePane(2,guildCenter);ReplacePane(3,guildRight);MoveDock(guildPane,3);status=guildStatus;headerSymbol.Content=Ui.Icon("hash",22);inviteButton!.IsVisible=true;draft.IsEnabled=sendButton!.IsEnabled=true;
    }
    void ShowHome()
    {
        activeGroup=null;activeDirect=null;if(createGroupButton!=null)createGroupButton.IsVisible=false;UpdateCompactCall();communityList.SelectedItem=null;activeCommunityId=null;selected=null;selectionVersion++;ReplacePane(1,homeSidebar);ReplacePane(2,homeCenter);ReplacePane(3,homeRight);MoveDock(homePane,2);status=homeStatus;RenderFriends();_ = Safe(RefreshFriends);
    }
    void BuildHome()
    {
        var friendsButton=Button("Amigos",()=>{ShowHome();return Task.CompletedTask;});friendsButton.Background=Ui.Card;friendsButton.HorizontalAlignment=HorizontalAlignment.Stretch;friendsButton.HorizontalContentAlignment=HorizontalAlignment.Left;friendsButton.Margin=new Thickness(10);friendsButton.Name="FriendsHome";
        var directHeader=Ui.Columns("*,Auto",Ui.Label("Mensagens diretas",12,Ui.Muted),IconButton("plus","Adicionar amigos",AddFriend));directHeader.Margin=new Thickness(16,16,10,8);
        var sidebarContent=Ui.Stack(3,directHeader,directList);homePane=new Grid{RowDefinitions=new RowDefinitions("Auto,*,Auto")};homePane.Children.Add(friendsButton);var scroll=new ScrollViewer{Content=sidebarContent};Grid.SetRow(scroll,1);homePane.Children.Add(scroll);
        homeSidebar=new Border{Background=Ui.Sidebar,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,1,0),Child=homePane};
        var tabs=new WrapPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};tabs.Children.Add(new Border{Child=Ui.Columns("Auto,Auto",Ui.Icon("users",20),Ui.Label("Amigos",15)),Margin=new Thickness(2,0,15,0)});
        foreach(var (label,key) in new[]{("Disponível","online"),("Todos","all"),("Pendentes","pending")}){var b=Button(label,()=>{friendFilter=key;RenderFriends();return Task.CompletedTask;});b.Name="FriendsTab_"+key;b.Padding=new Thickness(9,7);b.Background=Brushes.Transparent;b.FontSize=12;b.Margin=new Thickness(2,0);tabs.Children.Add(b);}
        var add=Button("Adicionar amigos",AddFriend,true);add.Name="AddFriendButton";add.Padding=new Thickness(11,7);add.FontSize=12;add.Margin=new Thickness(8,0);tabs.Children.Add(add);
        var toolbar=new Border{Child=tabs,Height=56,Padding=new Thickness(16,0),BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1)};
        friendsSearch.Name="FriendsSearch";friendsSearch.TextChanged+=(_,_)=>RenderFriends();friendsSearch.Margin=new Thickness(0,0,0,18);
        homeStatus=Ui.Label("",11,Ui.Muted);var content=Ui.Stack(8,friendsSearch,homeStatus,friendsCount,friendRows);content.Margin=new Thickness(24,18);
        var center=new Grid{RowDefinitions=new RowDefinitions("Auto,*")};center.Children.Add(toolbar);var list=new ScrollViewer{Content=content};Grid.SetRow(list,1);center.Children.Add(list);homeCenter=center;
        var info=Ui.Stack(20,Ui.Label("Sua rede",19),Ui.Label(connection.Connected?"● LAN / Radmin":"○ Rede offline",12,connection.Connected?Ui.Green:Ui.Muted),Ui.Label("Adicione seus amigos pelo nome para começar uma conversa privada.",13,Ui.Muted),Ui.Box(Ui.Stack(8,Ui.Label("As conversas ficam salvas",14),Ui.Label("Você pode fechar a conversa e encontrar o histórico aqui quando voltar à mesma rede.",12,Ui.Muted)),Ui.Card,14));info.Margin=new Thickness(18,24);
        homeRight=new Border{Background=Ui.Sidebar,BorderBrush=Ui.Line,BorderThickness=new Thickness(1,0,0,0),Child=info};
    }
    async Task RefreshFriends()
    {
        if(connection.Connected&&profile.PendingAvatarSync){var synced=await connection.UpdateProfile(new(profile.Name,profile.Avatar));profile.PendingAvatarSync=false;ApplyProfileChange(synced);}
        var items=connection.Connected?await connection.Get<List<FriendEntry>>("/api/friends"):cache.Friends.Select(f=>f with{Online=false,InCall=false}).ToList();friends.Clear();friends.AddRange(items);if(connection.Connected){cache.Friends=items;cache.Save();}
        connectionLabel.Text=connection.Connected?"● Conectado":"○ Rede offline";connectionLabel.Foreground=connection.Connected?Ui.Green:Ui.Muted;
        if(activeDirect!=null){var updated=friends.FirstOrDefault(f=>f.Id==activeDirect.Id);if(updated==null){ShowHome();return;}activeDirect=updated;SetDirectState();}
        RenderFriends();await FlushOutbox();
    }
    void RenderFriends()
    {
        directList.Children.Clear();foreach(var friend in friends){
            var open=Button("",()=>OpenDirect(friend));open.HorizontalAlignment=HorizontalAlignment.Stretch;open.HorizontalContentAlignment=HorizontalAlignment.Stretch;open.Padding=new Thickness(10,8);open.Margin=new Thickness(7,0);open.Background=activeDirect?.Id==friend.Id?Ui.Card:Brushes.Transparent;
            open.Name="DirectPeer_"+friend.Id;
            var detail=friend.State=="pending"?(friend.Incoming?"Solicitação recebida":"Solicitação enviada"):(friend.InCall?"Em chamada":friend.Online?"Disponível":"Offline");var count=unread.GetValueOrDefault(friend.Id);
            open.Content=Ui.Columns("Auto,*,Auto",Ui.Avatar(friend.Avatar,friend.Name,31),new Border{Margin=new Thickness(9,0,0,0),Child=Ui.Stack(2,Ui.Label(friend.Name,13),Ui.Label(detail,10,Ui.Muted))},Ui.Label(count>0?count.ToString():"",11,Ui.Accent));directList.Children.Add(open);
        }
        RenderGroupLinks();
        var filter=friendsSearch.Text?.Trim()??"";
        var visible=friends.Where(f=>f.Name.Contains(filter,StringComparison.OrdinalIgnoreCase)&&(friendFilter=="pending"?f.State=="pending":f.State=="accepted"&&(friendFilter!="online"||f.Online))).ToArray();
        friendsCount.Text=(friendFilter=="pending"?"PENDENTES":friendFilter=="online"?"DISPONÍVEIS":"TODOS OS AMIGOS")+" — "+visible.Length;friendRows.Children.Clear();
        foreach(var friend in visible){
            var row=new Grid{ColumnDefinitions=new ColumnDefinitions("Auto,*,Auto"),Margin=new Thickness(0,10)};row.Children.Add(Ui.Avatar(friend.Avatar,friend.Name,38));var label=new Border{Margin=new Thickness(12,0),Child=Ui.Stack(4,Ui.Label(friend.Name,14),Ui.Label(friend.State=="pending"?(friend.Incoming?"Quer ser seu amigo":"Aguardando resposta"):(friend.InCall?"Em chamada":friend.Online?"● Disponível":"Offline"),11,friend.Online?Ui.Green:Ui.Muted))};Grid.SetColumn(label,1);row.Children.Add(label);
            var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};if(friend.State=="pending"&&friend.Incoming){var accept=Button("Aceitar",()=>AcceptFriend(friend),true);accept.Name="AcceptFriend_"+friend.Id;actions.Children.Add(accept);actions.Children.Add(Button("Recusar",async()=>{await connection.Delete($"/api/friends/{friend.Id}");await RefreshFriends();}));}else actions.Children.Add(Button(friend.State=="accepted"?"Mensagem":"Abrir DM",()=>OpenDirect(friend)));
            Grid.SetColumn(actions,2);row.Children.Add(actions);friendRows.Children.Add(new Border{Child=row,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1)});
        }
        if(visible.Length==0)friendRows.Children.Add(new Border{Padding=new Thickness(18,45),Child=Ui.Stack(10,Ui.Label(friendFilter=="pending"?"Nenhuma solicitação pendente":"Sua turma começa aqui",21),Ui.Label("Use Adicionar amigos e digite o nome de alguém que esteja na mesma LAN ou rede Radmin.",13,Ui.Muted))});
    }
    async Task AddFriend()
    {
        var dialog=new Window{Title="Adicionar amigos",Width=470,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);var name=Ui.Field("Nome exato do seu amigo");name.Name="FriendName";name.MaxLength=32;
        var add=Ui.Action("Adicionar amigo",true);add.Name="SendFriendRequest";add.Click+=(_,_)=>{if(NameRules.Valid(name.Text))dialog.Close(name.Text!.Trim());};dialog.Content=new Border{Padding=new Thickness(28),Child=Ui.Stack(18,Ui.Label("Encontre sua turma",25),Ui.Label("Digite o nome usado pela pessoa na sua LAN / Radmin. Ela receberá uma solicitação para aceitar.",13,Ui.Muted),name,add)};
        var target=await dialog.ShowDialog<string?>(this);if(target==null)return;
        var entry=await connection.PostResult<FriendEntry,FriendRequest>("/api/friends",new(target));await RefreshFriends();await OpenDirect(entry);status.Text=entry.State=="accepted"?"Amizade confirmada. Vocês já podem conversar.":"Solicitação enviada. A DM foi criada; aguarde seu amigo aceitar para enviar mensagens.";
    }
    async Task AcceptFriend(FriendEntry friend){await connection.Post($"/api/friends/{friend.Id}/accept",new{});await RefreshFriends();await OpenDirect(friends.Single(f=>f.Id==friend.Id));}
    async Task OpenDirect(FriendEntry friend)
    {
        friend=friends.FirstOrDefault(f=>f.Id==friend.Id)??friend;
        communityList.SelectedItem=null;activeCommunityId=null;selected=null;var version=++selectionVersion;activeGroup=null;activeDirect=friend;
        ReplacePane(1,homeSidebar);ReplacePane(2,guildCenter);MoveDock(homePane,2);status=guildStatus;inviteButton!.IsVisible=false;
        title.Text=friend.Name;headerSymbol.Content=Ui.Avatar(friend.Avatar,friend.Name,28);search.Text="";draft.Text="";draft.Watermark="Mensagem para @"+friend.Name;messages.Clear();messageIds.Clear();unread.Remove(friend.Id);SetDirectState();RenderFriends();_ = Safe(RefreshCallActivity);
        var old=connection.Connected?await connection.Get<List<DirectMessage>>($"/api/direct/{friend.Id}/messages"):cache.Messages.GetValueOrDefault(friend.Id,[]);if(connection.Connected){cache.Messages[friend.Id]=old;cache.Save();}if(version!=selectionVersion||activeDirect?.Id!=friend.Id)return;
        var received=messages.ToArray();messages.Clear();messageIds.Clear();foreach(var item in old)AppendDirect(item);foreach(var item in received)Append(item);foreach(var pending in cache.Outbox.Where(p=>p.PeerId==friend.Id))AppendPending(pending);
    }
    void SetDirectState()
    {
        UpdateCompactCall();if(createGroupButton!=null)createGroupButton.IsVisible=activeDirect?.State=="accepted";if(activeDirect==null)return;var friend=activeDirect;var accepted=friend.State=="accepted";draft.IsEnabled=sendButton!.IsEnabled=accepted;
        subtitle.Text=!connection.Connected?"Rede offline · envio em fila local":accepted?(friend.InCall?"Em chamada":friend.Online?"Disponível":"Offline · mensagens serão guardadas"):"Solicitação de amizade pendente";
        var info=Ui.Stack(16,Ui.Avatar(friend.Avatar,friend.Name,76),Ui.Label(friend.Name,23),Ui.Label(subtitle.Text??"",12,Ui.Muted),Ui.Label("Este é o começo da conversa privada de vocês. As mensagens ficam salvas na sua rede.",13,Ui.Muted));
        if(!accepted&&friend.Incoming)info.Children.Add(Button("Aceitar amizade",()=>AcceptFriend(friend),true));else if(!accepted)info.Children.Add(Ui.Label("Seu amigo precisa aceitar a solicitação antes de vocês conversarem.",12,Ui.Muted));
        ReplacePane(3,new Border{Padding=new Thickness(18,24),Child=info,Background=Ui.Sidebar,BorderBrush=Ui.Line,BorderThickness=new Thickness(1,0,0,0)});
    }
    void AppendDirect(DirectMessage message){cache.Put(message,connection.Session!.UserId);Append(new(message.Id,0,message.SenderId,message.Author,message.Text,message.SentAt));}
    void ReceiveDirect(DirectMessage message)
    {
        var other=message.SenderId==connection.Session?.UserId?message.RecipientId:message.SenderId;
        var pending=cache.Outbox.FirstOrDefault(p=>p.ClientId==message.ClientId);cache.Put(message,connection.Session!.UserId);if(pending!=null&&activeDirect?.Id==other)RemovePendingView(pending.LocalId);
        if(activeDirect?.Id==other)AppendDirect(message);else if(message.SenderId!=connection.Session?.UserId){unread[other]=unread.GetValueOrDefault(other)+1;RenderFriends();}
    }
}
