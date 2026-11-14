using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using System.Collections.ObjectModel;
using JanjaCord.Client.Media;
using JanjaCord.Shared;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    readonly ObservableCollection<Channel> textChannels=[];
    readonly StackPanel voiceChannels=new(){Spacing=2};
    readonly TextBox search=Ui.Field("Buscar no canal");
    readonly TextBlock serverHeading=Ui.Label("Seu servidor",15),memberHeading=Ui.Label("MEMBROS",11,Ui.Muted);
    Grid? shell;
    Button? serverMenuButton;
    Flyout? serverFlyout;
    long? activeCommunityId;
    int communityVersion;
    Control guildSidebar=null!,guildCenter=null!,guildRight=null!,sharedDock=null!,homeSidebar=null!,homeCenter=null!,homeRight=null!;
    Grid guildPane=null!,homePane=null!;
    TextBlock guildStatus=null!,homeStatus=null!;
    readonly ContentControl headerSymbol=new();
    Button? inviteButton,sendButton;
    readonly TextBlock connectionLabel=Ui.Label("",10,Ui.Green);
    readonly ContentControl personalAvatar=new();
    readonly TextBlock personalName=Ui.Label("",13);
    Button IconButton(string icon,string tooltip,Func<Task> action)
    {
        var b=Button("",action);b.Content=Ui.Icon(icon,19);b.Padding=new Thickness(7);b.Background=Brushes.Transparent;ToolTip.SetTip(b,tooltip);Avalonia.Automation.AutomationProperties.SetName(b,tooltip);return b;
    }
    static Border Line()=>new(){Height=1,Background=Ui.Line,Margin=new Thickness(12,10)};
    void ShowChat()
    {
        call=new CallService(connection){InputDeviceId=profile.InputDeviceId,OutputDeviceId=profile.OutputDeviceId,VolumeForUser=PeerVolume,NoiseSuppressionAdvanced=profile.NoiseSuppressionAdvanced,NoiseSuppressionEnabled=profile.NoiseSuppressionEnabled,NoiseSuppressionStrength=profile.NoiseSuppressionStrength};ConnectDirectCalls();call.Error+=Report;call.Changed+=()=>Dispatcher.UIThread.Post(UpdateCallControls);
        call.Participants+=roster=>Dispatcher.UIThread.Post(()=>RenderRoster(roster));call.Frame+=(id,kind,pixels)=>frameQueue.Put(id,kind,pixels);
        call.State+=(id,state)=>Dispatcher.UIThread.Post(()=>{
            if(state=="screen-on")activeScreens.Add(id);else if(state=="screen-off")activeScreens.Remove(id);
            if(state.EndsWith("-off")){var key=id+":"+state[..^4];suppressedVideo.Add(key);if(videoTiles.Remove(key,out var tile)){DetachVideo(tile);tile.Dispose();}}
            else if(state.EndsWith("-on"))suppressedVideo.Remove(id+":"+state[..^3]);
            else if(state=="Failed")status.Text="Falha na mídia. Confira a conexão VPN e o firewall.";
            UpdateCompactCall();
        });
        connection.Message+=message=>Dispatcher.UIThread.Post(()=>{if(message.ChannelId==selected?.Id)Append(message);});connection.Status+=Report;
        connection.CommunityDeleted+=id=>Dispatcher.UIThread.Post(()=>HandleCommunityDeleted(id));
        connection.FriendsChanged+=()=>Dispatcher.UIThread.Post(()=>_ = Safe(RefreshFriends));
        connection.DirectMessageReceived+=message=>Dispatcher.UIThread.Post(()=>ReceiveDirect(message));
        connection.DirectGroupsChanged+=()=>Dispatcher.UIThread.Post(()=>_ = Safe(RefreshGroups));
        connection.GroupMessageReceived+=message=>Dispatcher.UIThread.Post(()=>ReceiveGroup(message));
        connection.GroupCallReceived+=invite=>Dispatcher.UIThread.Post(()=>_ = Safe(()=>ShowGroupRing(invite)));
        connection.FriendsChanged+=()=>Dispatcher.UIThread.Post(()=>_ = Safe(RefreshGroups));
        connection.FriendsChanged+=()=>Dispatcher.UIThread.Post(()=>_ = Safe(RefreshCallActivity));
        connection.DirectGroupsChanged+=()=>Dispatcher.UIThread.Post(()=>_ = Safe(RefreshCallActivity));
        connection.CallActivityChanged+=activity=>Dispatcher.UIThread.Post(()=>ReceiveCallActivity(activity));
        connection.CallPresenceChanged+=presence=>Dispatcher.UIThread.Post(()=>ReceiveCallPresence(presence));
        connection.ProfileChanged+=updated=>Dispatcher.UIThread.Post(()=>ApplyProfileChange(updated));
        connection.Interrupted+=()=>Dispatcher.UIThread.Post(()=>{if(shuttingDown)return;connectionLabel.Text="○ Rede offline";connectionLabel.Foreground=Ui.Muted;for(var i=0;i<friends.Count;i++)friends[i]=friends[i] with{Online=false};SetDirectState();RenderFriends();StartOfflineRetry();});
        communityList.ItemsSource=communities;communityList.Width=60;communityList.SelectionChanged+=async(_,_)=>await Safe(RefreshChannels);
        communityList.Styles.Add(new Style(s=>s.OfType<ListBoxItem>()){Setters={new Setter(ListBoxItem.PaddingProperty,new Thickness(5)),new Setter(ListBoxItem.MarginProperty,new Thickness(0,3)),new Setter(ListBoxItem.BorderThicknessProperty,new Thickness(3,0,0,0)),new Setter(ListBoxItem.BorderBrushProperty,Brushes.Transparent)}});
        communityList.Styles.Add(new Style(s=>s.OfType<ListBoxItem>().Class(":selected")){Setters={new Setter(ListBoxItem.BorderBrushProperty,Ui.Text)}});
        communityList.ItemTemplate=new FuncDataTemplate<Community>((community,_)=>{
            var avatar=Ui.Avatar(null,community?.Name??"J",43);if(community!=null){ToolTip.SetTip(avatar,community.Name);avatar.ContextMenu=ServerMenu(community);}return avatar;
        });
        var createServer=IconButton("plus","Criar servidor",CreateCommunity);createServer.Name="CreateServer";createServer.Background=Ui.Card;createServer.CornerRadius=new CornerRadius(22);createServer.Width=createServer.Height=42;
        var joinServer=IconButton("join","Entrar em servidor",JoinCommunity);joinServer.Background=Ui.Card;joinServer.CornerRadius=new CornerRadius(22);joinServer.Width=joinServer.Height=42;
        joinServer.Name="JoinServerButton";
        var railTop=Button("J",()=>{ShowHome();return Task.CompletedTask;});railTop.Name="HomeButton";railTop.Background=Ui.Brush("#5B4BB2");railTop.Width=42;railTop.Height=42;railTop.CornerRadius=new CornerRadius(15);ToolTip.SetTip(railTop,"Amigos e mensagens diretas");
        var rail=Ui.Stack(6,railTop,Line(),communityList,createServer,joinServer);rail.HorizontalAlignment=HorizontalAlignment.Center;rail.Margin=new Thickness(0,10);
        serverMenuButton=Ui.Action("");serverMenuButton.Click+=(_,_)=>{if(communityList.SelectedItem is Community c){serverFlyout?.Hide();serverFlyout=new Flyout{Content=ServerActions(c)};serverFlyout.ShowAt(serverMenuButton);}};
        serverMenuButton.Name="ServerMenu";serverMenuButton.Content=Ui.Columns("*,Auto",serverHeading,Ui.Icon("chevron",17));serverMenuButton.Background=Brushes.Transparent;serverMenuButton.HorizontalAlignment=HorizontalAlignment.Stretch;serverMenuButton.HorizontalContentAlignment=HorizontalAlignment.Stretch;serverMenuButton.Padding=new Thickness(16,15);serverMenuButton.CornerRadius=new CornerRadius(0);
        channelList.ItemsSource=textChannels;channelList.ItemTemplate=new FuncDataTemplate<Channel>((c,_)=>{
            var row=Ui.Columns("Auto,*",Ui.Icon("hash",19),new TextBlock{Text=c?.Name,Foreground=Ui.Text,Margin=new Thickness(9,0),FontSize=14,VerticalAlignment=VerticalAlignment.Center});return row;
        });channelList.SelectionChanged+=async(_,_)=>await Safe(()=>SelectChannel());
        var channelSections=Ui.Stack(6,SectionTitle("CONVERSAS"),channelList,SectionTitle("CANAIS DE VOZ"),voiceChannels);channelSections.Margin=new Thickness(8,18,8,10);
        var createChannel=Button("+  Criar canal",CreateChannel);createChannel.Name="CreateChannel";createChannel.FontSize=12;createChannel.Padding=new Thickness(10,7);createChannel.Background=Brushes.Transparent;createChannel.HorizontalAlignment=HorizontalAlignment.Stretch;
        var refresh=IconButton("refresh","Atualizar canais e membros",RefreshChannels);
        var channelActions=Ui.Columns("*,Auto",createChannel,refresh);channelActions.Margin=new Thickness(8,4);
        connectionLabel.Name="ConnectionStatus";connectionLabel.Text=connection.Connected?"● Conectado":"○ Rede offline";connectionLabel.Margin=new Thickness(9,0);
        personalAvatar.Content=Ui.Avatar(profile.Avatar,profile.Name,33);personalName.Text=profile.Name;personalName.Margin=new Thickness(9,0);personalName.FontWeight=FontWeight.SemiBold;personalName.TextWrapping=TextWrapping.NoWrap;personalName.TextTrimming=TextTrimming.CharacterEllipsis;
        var user=Button("",EditProfile);user.Name="EditProfileButton";user.Content=Ui.Columns("Auto,*",personalAvatar,Ui.Stack(2,personalName,connectionLabel));user.Background=Brushes.Transparent;user.Padding=new Thickness(0);user.HorizontalContentAlignment=HorizontalAlignment.Stretch;ToolTip.SetTip(user,"Editar meu nome e minha foto");
        micButton=IconButton("mic","Silenciar ou ativar microfone",()=>call!.InCall?call.ToggleMute():Task.CompletedTask);micButton.Name="MicrophoneButton";AddMicrophoneSettings(micButton);deafenButton=IconButton("audio","Ligar ou desligar áudio recebido",()=>call!.ToggleDeafen());
        cameraButton=IconButton("camera","Ligar ou desligar câmera",ToggleCamera);shareButton=IconButton("screen","Compartilhar tela",ToggleShare);leaveButton=IconButton("leave","Sair da chamada",EndCurrentCall);
        var mediaButtons=Ui.Columns("*,*,*",cameraButton,shareButton,leaveButton);mediaButtons.Margin=new Thickness(0,8,0,0);
        callLabel.FontSize=12;callLabel.Foreground=Ui.Green;
        var voicePanel=Ui.Stack(6,callLabel,callMembers,mediaButtons);voicePanel.Margin=new Thickness(12,12);var voiceBorder=new Border{Child=voicePanel,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,1,0,0)};
        var personal=Ui.Columns("*,Auto,Auto",user,micButton,deafenButton);personal.Margin=new Thickness(10,10);var personalBorder=new Border{Child=personal,Background=Ui.Brush("#151518"),BorderBrush=Ui.Line,BorderThickness=new Thickness(0,1,0,0)};
        sharedDock=Ui.Stack(0,voiceBorder,personalBorder);
        var channelPane=new Grid{RowDefinitions=new RowDefinitions("Auto,*,Auto,Auto")};guildPane=channelPane;channelPane.Children.Add(serverMenuButton);var scrollChannels=new ScrollViewer{Content=channelSections};Grid.SetRow(scrollChannels,1);channelPane.Children.Add(scrollChannels);Grid.SetRow(channelActions,2);channelPane.Children.Add(channelActions);Grid.SetRow(sharedDock,3);channelPane.Children.Add(sharedDock);
        serverHeading.Name="ServerHeading";title.Name="ChannelHeading";title.FontSize=15;title.FontWeight=FontWeight.SemiBold;subtitle.FontSize=11;subtitle.MaxWidth=270;
        var invite=IconButton("users","Copiar ID para convidar",async()=>{if(communityList.SelectedItem is Community c)await CopyInvite(c);});
        inviteButton=invite;
        var toggleMembers=IconButton("users","Mostrar ou ocultar membros",()=>{if(shell!=null)shell.ColumnDefinitions[3].Width=shell.ColumnDefinitions[3].Width.Value==0?new GridLength(224):new GridLength(0);return Task.CompletedTask;});
        search.Width=185;search.FontSize=12;search.Padding=new Thickness(10,6);search.TextChanged+=(_,_)=>ApplySearch();
        headerSymbol.Content=Ui.Icon("hash",22);
        directCallButton=IconButton("phone","Ligar para seu amigo",StartDirect);directCallButton.Name="StartDirectCall";directCallButton.IsVisible=false;
        createGroupButton=IconButton("user-plus","Criar grupo com amigos",CreateGroup);createGroupButton.Name="CreateDirectGroup";createGroupButton.IsVisible=false;
        var top=Ui.Columns("Auto,*,Auto,Auto,Auto,Auto,Auto",headerSymbol,new Border{Margin=new Thickness(10,0),Child=Ui.Stack(2,title,subtitle)},invite,directCallButton,createGroupButton,toggleMembers,search);top.VerticalAlignment=VerticalAlignment.Center;top.Margin=new Thickness(16,0,12,0);
        var topBorder=new Border{Height=56,Child=top,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1)};
        history.ItemsSource=messages;history.ItemTemplate=new FuncDataTemplate<ChatMessage>((message,_)=>MessageView(message!));history.Padding=new Thickness(6,8);history.SelectionMode=SelectionMode.Single;
        var mediaScroll=new ScrollViewer{Content=videoGrid,MaxHeight=310,Margin=new Thickness(12,6)};
        var send=IconButton("send","Enviar mensagem",Send);send.Background=Ui.Card;send.Width=40;
        send.Name="SendMessage";draft.Name="MessageDraft";
        sendButton=send;
        draft.MaxLength=4000;draft.Background=Ui.Brush("#151518");draft.BorderThickness=new Thickness(0);draft.Padding=new Thickness(12);draft.KeyDown+=async(_,e)=>{if(e.Key==Avalonia.Input.Key.Enter){e.Handled=true;await Safe(Send);}};
        var composer=Ui.Columns("*,Auto",draft,send);var composerBorder=new Border{Child=composer,Background=Ui.Brush("#151518"),CornerRadius=new CornerRadius(8),BorderBrush=Ui.Line,BorderThickness=new Thickness(1),Margin=new Thickness(16,8,16,4)};
        var footer=Ui.Stack(4,composerBorder,NewStatus());footer.Margin=new Thickness(0,0,0,7);status.Margin=new Thickness(18,0);status.FontSize=10;
        guildStatus=status;
        var center=new Grid{RowDefinitions=new RowDefinitions("Auto,Auto,Auto,*,Auto")};center.Children.Add(topBorder);var compact=BuildCompactCall();Grid.SetRow(compact,1);center.Children.Add(compact);Grid.SetRow(mediaScroll,2);center.Children.Add(mediaScroll);Grid.SetRow(history,3);center.Children.Add(history);Grid.SetRow(footer,4);center.Children.Add(footer);
        var right=Ui.Stack(20,memberHeading,memberList);right.Margin=new Thickness(16,22);var rightBorder=new Border{Child=new ScrollViewer{Content=right},Background=Ui.Sidebar,BorderBrush=Ui.Line,BorderThickness=new Thickness(1,0,0,0)};
        guildSidebar=new Border{Background=Ui.Sidebar,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,1,0),Child=channelPane};guildCenter=center;guildRight=rightBorder;
        shell=Ui.Columns("64,252,*,224",new Border{Background=Ui.Brush("#050506"),Child=new ScrollViewer{Content=rail}},guildSidebar,center,rightBorder);Content=shell;
        BuildHome();UpdateTrayLabel();_ = Safe(RefreshGroups);_ = Safe(RefreshCallActivity);UpdateCallControls();ShowHome();StartOutboxRetry();
    }
    static Control SectionTitle(string text)=>new Border{Margin=new Thickness(8,8,0,5),Child=Ui.Label("⌄  "+text,10,Ui.Muted)};
    void ApplySearch(){var value=search.Text?.Trim();history.ItemsSource=string.IsNullOrEmpty(value)?messages:messages.Where(m=>m.Text.Contains(value,StringComparison.OrdinalIgnoreCase)||m.Author.Contains(value,StringComparison.OrdinalIgnoreCase)).ToArray();}
    async Task CopyInvite(Community community){if(Clipboard!=null)await Clipboard.SetTextAsync(community.Id.ToString());status.Text=$"ID {community.Id} copiado. Compartilhe também o endereço do host com seu amigo.";}
    ContextMenu ServerMenu(Community community)
    {
        var invite=new MenuItem{Header="Convidar pelo ID"};invite.Click+=async(_,_)=>await Safe(()=>CopyInvite(community));
        var create=new MenuItem{Header="Criar canal",IsVisible=community.OwnerId==connection.Session?.UserId};create.Click+=async(_,_)=>await Safe(async()=>{communityList.SelectedItem=community;await CreateChannel();});
        var refresh=new MenuItem{Header="Atualizar servidor"};refresh.Click+=async(_,_)=>await Safe(RefreshChannels);
        var delete=new MenuItem{Header="Excluir servidor",Name="DeleteServer",Foreground=Ui.Red,IsVisible=community.OwnerId==connection.Session?.UserId};delete.Click+=async(_,_)=>await Safe(()=>DeleteCommunity(community));
        return new ContextMenu{ItemsSource=new object[]{invite,create,refresh,new Separator(),delete}};
    }
    Control ServerActions(Community community)
    {
        Button Action(string text,Func<Task> run){var b=Button(text,async()=>{serverFlyout?.Hide();await run();});b.Background=Brushes.Transparent;b.HorizontalAlignment=HorizontalAlignment.Stretch;b.HorizontalContentAlignment=HorizontalAlignment.Left;return b;}
        var invite=Action("Convidar pelo ID",()=>CopyInvite(community));
        var create=Action("Criar canal",CreateChannel);create.IsVisible=community.OwnerId==connection.Session?.UserId;
        var refresh=Action("Atualizar servidor",RefreshChannels);
        var delete=Action("Excluir servidor",()=>DeleteCommunity(community));delete.Name="DeleteServer";delete.Foreground=Ui.Red;delete.IsVisible=community.OwnerId==connection.Session?.UserId;
        return new StackPanel{Width=210,Spacing=3,Children={invite,create,refresh,new Separator(),delete}};
    }
    async Task DeleteCommunity(Community community)
    {
        if(community.OwnerId!=connection.Session?.UserId)throw new Exception("Somente o dono pode excluir este servidor.");
        var dialog=new Window{Title="Excluir servidor",Width=460,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);
        var field=Ui.Field(community.Name);field.Name="DeleteConfirmation";
        var confirm=Ui.Action("Excluir servidor");confirm.Name="ConfirmDelete";confirm.Background=Ui.Brush("#AE3545");confirm.Foreground=Brushes.White;confirm.IsEnabled=false;
        field.TextChanged+=(_,_)=>confirm.IsEnabled=string.Equals(field.Text,community.Name,StringComparison.Ordinal);
        confirm.Click+=(_,_)=>dialog.Close(true);var cancel=Ui.Action("Cancelar");cancel.Click+=(_,_)=>dialog.Close(false);
        dialog.Content=new Border{Padding=new Thickness(26),Child=Ui.Stack(16,Ui.Label("Excluir "+community.Name+"?",24),Ui.Label("Todos os canais e mensagens serão apagados permanentemente. As chamadas serão encerradas para todos os participantes.",14,Ui.Muted),Ui.Label("Digite o nome do servidor para confirmar:",12),field,Ui.Columns("*,*",cancel,confirm))};
        if(!await dialog.ShowDialog<bool>(this))return;
        await connection.Delete($"/api/communities/{community.Id}");HandleCommunityDeleted(community.Id);
    }
    void HandleCommunityDeleted(long id)
    {
        var wasActive=activeCommunityId==id;var community=communities.FirstOrDefault(c=>c.Id==id);if(wasActive)communityList.SelectedItem=null;if(community!=null)communities.Remove(community);
        if(wasActive){communityVersion++;selectionVersion++;activeCommunityId=null;selected=null;channels.Clear();textChannels.Clear();voiceChannels.Children.Clear();messages.Clear();messageIds.Clear();members.Clear();memberList.Children.Clear();serverHeading.Text="Seu servidor";title.Text="Selecione um servidor";subtitle.Text="";ShowHome();}
        status.Text="O servidor foi excluído.";
    }
}
