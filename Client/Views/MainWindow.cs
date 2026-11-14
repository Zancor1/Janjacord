using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using JanjaCord.Client.Media;
using JanjaCord.Client.Services;
using JanjaCord.Client.Networking;
using JanjaCord.Shared;
using Microsoft.MixedReality.WebRTC;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow:Window
{
    Connection connection=new();
    CallService? call;
    readonly LocalProfile profile=LocalProfile.Load();
    readonly AutomaticNetwork automaticNetwork=new();
    ConversationCache cache=new();
    DispatcherTimer? offlineRetry;
    bool retrying;
    bool shuttingDown;
    bool editingProfile;
    TextBlock status=Ui.Label("",12,Ui.Muted);
    readonly ObservableCollection<Community> communities=[];
    readonly ObservableCollection<Channel> channels=[];
    readonly ObservableCollection<ChatMessage> messages=[];
    readonly Dictionary<long,MemberProfile> members=[];
    readonly HashSet<long> messageIds=[];
    readonly ListBox communityList=new(),channelList=new(),history=new();
    readonly TextBox draft=Ui.Field("Envie uma mensagem para o grupo");
    readonly TextBlock title=Ui.Label("Sua turma está por aqui",20),subtitle=Ui.Label("Selecione um servidor e um canal para começar.",12,Ui.Muted);
    readonly StackPanel memberList=new(){Spacing=16};
    readonly WrapPanel videoGrid=new(){Orientation=Orientation.Horizontal};
    readonly WrapPanel callMembers=new(){Orientation=Orientation.Horizontal};
    readonly HashSet<string> suppressedVideo=[];
    readonly Dictionary<string,VideoTile> videoTiles=[];
    readonly ConcurrentFrameQueue frameQueue=new();
    TextBlock callLabel=Ui.Label("Nenhuma chamada ativa",12,Ui.Muted);
    Button? micButton,deafenButton,cameraButton,shareButton,leaveButton;
    Channel? selected;
    long? voiceChannel;
    int selectionVersion;
    readonly DispatcherTimer frameTimer;
    bool initialWindowOpened;
    public MainWindow()
    {
        Title="JanjaCord";Width=1320;Height=860;MinWidth=1000;MinHeight=680;Background=Ui.Background;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Ui.ApplyStyles(this);if(profile.ProtectedKey.Length>0&&NameRules.Valid(profile.Name)&&Environment.GetEnvironmentVariable("JANJACORD_MANUAL_CONNECT")!="1")ShowReturningProfile();else ShowProfile();
        Opened+=(_,_)=>{try{ConfigureTray();}catch(Exception ex){Report(ex.Message);}};
        Opened+=(_,_)=>{try{var handle=TryGetPlatformHandle();if(handle!=null){var dark=1;DwmSetWindowAttribute(handle.Handle,20,ref dark,sizeof(int));}}catch{/* Older Windows builds retain their system title bar. */}};
        Opened+=async(_,_)=>{if(initialWindowOpened)return;initialWindowOpened=true;if(profile.ProtectedKey.Length>0&&NameRules.Valid(profile.Name)&&Environment.GetEnvironmentVariable("JANJACORD_MANUAL_CONNECT")!="1")try{await ConnectAutomatically();}catch(Exception ex){ShowProfile();status.Text=ex.Message;}};
        frameTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1d/60)};frameTimer.Tick+=(_,_)=>RenderFrames();frameTimer.Start();
        Closed+=async(_,_)=>{try{shuttingDown=true;ringtone.Dispose();offlineRetry?.Stop();outboxRetry?.Stop();frameTimer.Stop();if(call!=null)await call.DisposeAsync();await connection.DisposeAsync();await automaticNetwork.DisposeAsync();foreach(var tile in videoTiles.Values)tile.Dispose();}finally{FinishShutdown();}};
    }
    Button Button(string text,Func<Task> action,bool primary=false)
    {
        var button=Ui.Action(text,primary);button.Click+=async(_,_)=>{button.IsEnabled=false;try{await action();}catch(Exception ex){Report(ex.Message);}finally{button.IsEnabled=true;}};return button;
    }
    void Report(string text){Dispatcher.UIThread.Post(()=>status.Text=text);}
    TextBlock NewStatus(){status=Ui.Label("",12,Ui.Muted);return status;}
    void ShowProfile()
    {
        var name=Ui.Field("Como seus amigos vão chamar você?",profile.Name);name.MaxLength=32;
        name.Name="ProfileName";
        var avatarHost=new ContentControl{Content=Ui.Avatar(profile.Avatar,profile.Name,96),HorizontalAlignment=HorizontalAlignment.Center};
        name.TextChanged+=(_,_)=>avatarHost.Content=Ui.Avatar(profile.Avatar,name.Text??"J",96);
        Button? next=null;
        var photo=Button("Escolher foto",async()=>{if(next!=null)next.IsEnabled=false;try{await ChoosePhoto();avatarHost.Content=Ui.Avatar(profile.Avatar,name.Text??"J",96);}finally{if(next!=null)next.IsEnabled=true;}});photo.Name="ChooseProfilePhoto";photo.HorizontalAlignment=HorizontalAlignment.Center;
        next=Button(string.IsNullOrWhiteSpace(profile.Name)?"Criar meu perfil  →":"Entrar  →",async()=>{
            if(!NameRules.Valid(name.Text))throw new Exception("Use de 2 a 32 caracteres: letras, números, espaço, ponto, hífen ou sublinhado.");
            profile.Name=NameRules.Normalize(name.Text!);profile.Save();
            if(Environment.GetEnvironmentVariable("JANJACORD_MANUAL_CONNECT")=="1"){ShowConnection();return;}
            status.Text="Procurando sua turma na LAN / Radmin…";
            await ConnectAutomatically();
        },true);next.HorizontalAlignment=HorizontalAlignment.Stretch;
        next.Name="CreateProfile";
        var advanced=Button("Conectar a uma rede por endereço",()=>{if(!NameRules.Valid(name.Text))throw new Exception("Escolha seu nome primeiro.");profile.Name=NameRules.Normalize(name.Text!);profile.Save();ShowConnection();return Task.CompletedTask;});advanced.Background=Brushes.Transparent;advanced.FontSize=11;
        var form=Ui.Stack(15,Ui.Label("SEU ESPAÇO, SUA GALERA",11,Ui.Accent),Ui.Label("Primeiro, seja você.",32),Ui.Label("Um nome, uma foto e boas conversas. Sua rede é encontrada automaticamente.",15,Ui.Muted),new Border{Height=8},avatarHost,photo,Ui.Label("SEU NOME",11,Ui.Muted),name,next,NewStatus(),advanced,Ui.Label("Seu perfil fica protegido pela sua conta do Windows neste computador.",12,Ui.Muted));
        form.MaxWidth=400;form.HorizontalAlignment=HorizontalAlignment.Stretch;form.VerticalAlignment=VerticalAlignment.Center;form.Margin=new Thickness(42);
        var hero=Ui.Stack(26,Brand(),new Border{Height=65},Ui.Label("Perto, mesmo\nquando longe.",54),Ui.Label("Sua voz. Sua tela. Seu grupo.\nTudo na sua própria rede.",19,Ui.Brush("#C8C1E5")),new Border{Height=25},Ui.Box(Ui.Stack(12,Ui.Label("Um lugar para ficar junto",20),Ui.Label("Converse enquanto joga, mostre o que está criando e compartilhe os momentos.",15,Ui.Brush("#C8C1E5"))),Ui.Brush("#322C49"),24),Ui.Label("JANJACORD  /  FEITO PARA SUA TURMA",11,Ui.Brush("#ACA1CD")));
        hero.Margin=new Thickness(48);hero.VerticalAlignment=VerticalAlignment.Center;
        var layout=Ui.Columns("*,*",new Border{Background=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,1,RelativeUnit.Relative),GradientStops={new GradientStop(Color.Parse("#302745"),0),new GradientStop(Color.Parse("#191B29"),1)}},Child=hero},new ScrollViewer{Content=form,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto});
        Content=layout;
    }
    static Control Brand()=>Ui.Columns("Auto,Auto",Ui.Box(Ui.Label("J",23),Ui.Accent,10,12),new TextBlock{Text="JanjaCord",FontSize=24,FontWeight=FontWeight.Bold,Foreground=Ui.Text,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0)});
    void ShowConnection()
    {
        var address=Ui.Field("IP do servidor Radmin",profile.Address);var pin=Ui.Field("Cole a impressão SHA-256 do servidor",profile.Fingerprint);pin.MaxLength=95;
        address.Name="ServerAddress";pin.Name="ServerFingerprint";
        var connect=Button("Entrar no JanjaCord  →",async()=>{
            var url=address.Text?.Trim()??"";if(!url.StartsWith("https://",StringComparison.OrdinalIgnoreCase))url="https://"+url;if(Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.IsDefaultPort)url=url.TrimEnd('/')+":5000";
            await EnterNetwork(url,pin.Text??"");
        },true);connect.HorizontalAlignment=HorizontalAlignment.Stretch;
        connect.Name="Connect";
        var card=Ui.Stack(20,Brand(),new Border{Height=8},Ui.Avatar(profile.Avatar,profile.Name,64),Ui.Label($"Tudo pronto, {profile.Name}.",28),Ui.Label("Agora, conecte-se ao computador que está hospedando seu grupo.",14,Ui.Muted),Ui.Label("ENDEREÇO DO SERVIDOR",11,Ui.Muted),address,Ui.Label("IMPRESSÃO DE SEGURANÇA",11,Ui.Muted),pin,Ui.Label("O host mostra essa impressão ao abrir Server.exe. Ela garante que você está falando com o servidor certo.",12,Ui.Muted),connect,Button("← Editar meu perfil",()=>{ShowProfile();return Task.CompletedTask;}),NewStatus());
        Content=new ScrollViewer{Content=new Border{MaxWidth=520,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(30),Child=Ui.Box(card,Ui.Sidebar,30,20)}};
    }
    async Task EnterNetwork(string address,string pin)
    {
        var candidate=new Connection();
        try{
            await candidate.LoginDevice(address,new(profile.Name,profile.DeviceKey(),profile.Avatar),pin);
            var list=await candidate.Get<List<Community>>("/api/communities");await connection.DisposeAsync();connection=candidate;
            communities.Clear();foreach(var item in list)communities.Add(item);
            profile.Address=address;profile.Fingerprint=pin;profile.UserId=candidate.Session!.UserId;profile.NetworkProtocol=6;profile.PendingAvatarSync=false;profile.Save();cache=ConversationCache.Load(pin);ShowChat();
        }catch{await candidate.DisposeAsync();throw;}
    }
    async Task ConnectAutomatically()
    {
        try{status.Text="Encontrando sua rede…";var endpoint=await automaticNetwork.Connect(profile);await EnterNetwork(endpoint.Address,endpoint.Host.Fingerprint);}
        catch(NetworkOfflineException ex){
            cache=ConversationCache.Load(profile.Fingerprint);connection.OfflineIdentity(profile.UserId,profile.Name);ShowChat();status.Text=ex.Message;
            StartOfflineRetry();
        }
    }
    void StartOfflineRetry()
    {
        if(shuttingDown||automaticNetwork.Hosting||offlineRetry?.IsEnabled==true)return;
        offlineRetry=new DispatcherTimer{Interval=TimeSpan.FromSeconds(12)};offlineRetry.Tick+=async(_,_)=>{
            if(retrying||shuttingDown||editingProfile||flushingOutbox)return;retrying=true;try{var endpoint=await automaticNetwork.Connect(profile);if(shuttingDown)return;profile.Address=endpoint.Address;profile.Save();offlineRetry.Stop();var window=new MainWindow();if(Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)desktop.MainWindow=window;window.Show();Close();}catch(NetworkOfflineException){}catch(Exception error){Report(error.Message);}finally{retrying=false;}
        };offlineRetry.Start();
    }
    async Task Safe(Func<Task> action){try{await action();}catch(Exception ex){Report(ex.Message);}}
    async Task<string?> AskName(string title,string placeholder)
    {
        var dialog=new Window{Title=title,Width=440,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);var field=Ui.Field(placeholder);field.MaxLength=60;
        field.Name="DialogName";var ok=Ui.Action("Continuar",true);ok.Name="DialogConfirm";ok.Click+=(_,_)=>{if(!string.IsNullOrWhiteSpace(field.Text))dialog.Close(field.Text.Trim());};var cancel=Ui.Action("Cancelar");cancel.Click+=(_,_)=>dialog.Close();dialog.Content=new Border{Padding=new Thickness(26),Child=Ui.Stack(18,Ui.Label(title,22),field,Ui.Columns("*,*",cancel,ok))};return await dialog.ShowDialog<string?>(this);
    }
    async Task CreateCommunity(){var name=await AskName("Crie seu servidor","Nome do seu grupo");if(name==null)return;await connection.Post("/api/communities",new CreateCommunity(name));await RefreshCommunities();communityList.SelectedIndex=communities.Count-1;}
    async Task JoinCommunity()
    {
        var dialog=new Window{Title="Entrar em servidor",Width=450,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);
        var field=Ui.Field("ID compartilhado pelo seu amigo");field.Name="JoinServerId";field.MaxLength=19;
        var error=Ui.Label("",12,Ui.Red);error.Name="JoinServerError";
        var join=Ui.Action("Entrar",true);join.Name="JoinServerConfirm";
        join.Click+=async(_,_)=>{
            if(!long.TryParse(field.Text,out var id)||id<=0){error.Text="Digite um ID de servidor válido, usando apenas números.";return;}
            join.IsEnabled=false;error.Text="";
            try{await connection.JoinCommunity(id);dialog.Close(id);}
            catch(Exception ex){error.Text=ex.Message;join.IsEnabled=true;}
        };
        var cancel=Ui.Action("Cancelar");cancel.Name="CancelJoinServer";cancel.Click+=(_,_)=>dialog.Close();
        dialog.Content=new Border{Padding=new Thickness(26),Child=Ui.Stack(16,Ui.Label("Entre no grupo",24),Ui.Label("Peça ao seu amigo o ID de um servidor desta rede.",13,Ui.Muted),field,error,Ui.Columns("*,*",cancel,join))};
        var selectedId=await dialog.ShowDialog<long?>(this);if(selectedId==null)return;await RefreshCommunities();communityList.SelectedItem=communities.FirstOrDefault(c=>c.Id==selectedId);
    }
    async Task CreateChannel()
    {
        if(communityList.SelectedItem is not Community community)throw new Exception("Selecione um servidor.");
        var dialog=new Window{Title="Criar canal",Width=440,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);
        var name=Ui.Field("Nome do canal");name.Name="ChannelName";name.MaxLength=60;var kind=new ComboBox{ItemsSource=new[]{"Texto","Voz, câmera e tela"},Name="ChannelKind",SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};var create=Ui.Action("Criar canal",true);create.Name="CreateChannelConfirm";create.Click+=(_,_)=>{if(!string.IsNullOrWhiteSpace(name.Text))dialog.Close(new CreateChannel(name.Text.Trim(),kind.SelectedIndex==0?"text":"voice"));};dialog.Content=new Border{Padding=new Thickness(26),Child=Ui.Stack(18,Ui.Label("Um novo lugar para o grupo",22),name,kind,create)};
        var input=await dialog.ShowDialog<CreateChannel?>(this);if(input==null)return;await connection.Post($"/api/communities/{community.Id}/channels",input);await RefreshChannels();
    }
    async Task RefreshCommunities(){var list=await connection.Get<List<Community>>("/api/communities");communities.Clear();foreach(var c in list)communities.Add(c);}
    async Task RefreshChannels()
    {
        if(communityList.SelectedItem is Community)ShowGuild();
        var version=++communityVersion;selectionVersion++;channels.Clear();textChannels.Clear();voiceChannels.Children.Clear();selected=null;messageIds.Clear();messages.Clear();members.Clear();memberList.Children.Clear();activeCommunityId=(communityList.SelectedItem as Community)?.Id;
        if(communityList.SelectedItem is not Community community)return;
        serverHeading.Text=community.Name;title.Text=community.Name;subtitle.Text=$"Servidor #{community.Id}";
        var list=await connection.Get<List<Channel>>($"/api/communities/{community.Id}/channels");
        var people=await connection.Get<List<MemberProfile>>($"/api/communities/{community.Id}/members");if(version!=communityVersion||activeCommunityId!=community.Id)return;
        foreach(var channel in list){channels.Add(channel);if(channel.Kind=="text")textChannels.Add(channel);else {var b=Button("",()=>SelectChannel(channel));b.Content=Ui.Columns("Auto,*",Ui.Icon("voice",18),new TextBlock{Text=channel.Name,Foreground=Ui.Muted,FontSize=14,Margin=new Thickness(9,0),VerticalAlignment=VerticalAlignment.Center});b.HorizontalAlignment=HorizontalAlignment.Stretch;b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.Background=Brushes.Transparent;b.Padding=new Thickness(10,8);voiceChannels.Children.Add(b);}}
        memberHeading.Text=$"MEMBROS — {people.Count}";
        foreach(var member in people.OrderByDescending(p=>p.Id==community.OwnerId).ThenBy(p=>p.Name)){
            members[member.Id]=member;var name=Ui.Stack(2,Ui.Label(member.Name,13),Ui.Label(member.Id==community.OwnerId?"Dono do servidor":"Membro",10,Ui.Muted));
            memberList.Children.Add(Ui.Columns("Auto,*",Ui.Avatar(member.Avatar,member.Name,34),new Border{Margin=new Thickness(9,0),Child=name,VerticalAlignment=VerticalAlignment.Center}));
        }
        status.Text=channels.Count==0?"Crie seu primeiro canal no menu do servidor.":"";
        if(textChannels.Count>0)channelList.SelectedIndex=0;
    }
    async Task SelectChannel(Channel? target=null)
    {
        activeDirect=null;
        var version=++selectionVersion;var channel=target??channelList.SelectedItem as Channel;if(channel==null)return;
        if(channel.Kind=="voice"){
            if(voiceChannel==channel.Id&&call!.InCall)return;
            status.Text="Entrando na chamada… o microfone será utilizado.";await call!.Join(channel.Id);voiceChannel=channel.Id;callLabel.Text="● Conectado a "+channel.Name;status.Text=call.Muted?"Você entrou apenas para ouvir. Confira o microfone padrão e as permissões do Windows; clique em Mic desligado para tentar novamente.":"Use fones de ouvido. Câmera e tela só ligam quando você escolher.";UpdateCallControls();return;
        }
        selected=channel;title.Text=channel.Name;subtitle.Text="Converse com sua turma";draft.Watermark="Conversar em # "+channel.Name;search.Text="";messages.Clear();messageIds.Clear();await connection.Watch(channel.Id);
        var old=await connection.Get<List<ChatMessage>>($"/api/channels/{channel.Id}/messages");if(version!=selectionVersion||selected?.Id!=channel.Id)return;
        var combined=old.Concat(messages).DistinctBy(m=>m.Id).OrderBy(m=>m.Id).ToArray();messages.Clear();messageIds.Clear();foreach(var m in combined)Append(m);
    }
    async Task Send()
    {
        var text=draft.Text;if(string.IsNullOrWhiteSpace(text))return;
        if(activeGroup is {} group){if(groupDraftAttempt is not {} attempt||attempt.Group!=group.Id||attempt.Text!=text)groupDraftAttempt=(group.Id,text,Guid.NewGuid().ToString());var message=await connection.SendGroup(group.Id,text,groupDraftAttempt.Value.ClientId);groupDraftAttempt=null;if(activeGroup?.Id==group.Id){AppendGroup(message);if(draft.Text==text)draft.Text="";}return;}
        if(activeDirect is {} peer){if(peer.State!="accepted")throw new Exception("Aceite a amizade primeiro.");var pending=cache.Enqueue(peer.Id,text);AppendPending(pending);draft.Text="";await FlushOutbox();return;}
        if(selected==null)throw new Exception("Selecione uma conversa para enviar uma mensagem.");var id=selected.Id;await connection.Send(id,text);if(selected?.Id==id&&draft.Text==text)draft.Text="";
    }
    void Append(ChatMessage message){if(!messageIds.Add(message.Id))return;messages.Add(message);ApplySearch();history.ScrollIntoView(message);}
    Control MessageView(ChatMessage? message)
    {
        if(message==null)return new Border();
        members.TryGetValue(message.UserId,out var member);var header=Ui.Columns("Auto,Auto",new TextBlock{Text=message.Author,Foreground=Ui.Text,FontWeight=FontWeight.SemiBold},new TextBlock{Text=message.SentAt.ToLocalTime().ToString("HH:mm"),Foreground=Ui.Muted,FontSize=11,Margin=new Thickness(10,2,0,0)});
        var avatar=member?.Avatar??(message.UserId==connection.Session?.UserId?profile.Avatar:friends.FirstOrDefault(f=>f.Id==message.UserId)?.Avatar);
        var body=new SelectableTextBlock{Text=message.Text,Foreground=Ui.Brush("#CECED4"),TextWrapping=TextWrapping.Wrap,FontSize=14};var row=Ui.Columns("Auto,*",Ui.Avatar(avatar,message.Author,38),new Border{Margin=new Thickness(12,0,0,0),Child=(message.Id<0&&cache.Outbox.FirstOrDefault(p=>p.LocalId==message.Id) is {} pending?Ui.Stack(4,header,body,PendingStatus(pending)):Ui.Stack(4,header,body))});row.Margin=new Thickness(6,3);return row;
    }
    async Task ToggleCamera()
    {
        if(!call!.InCall)throw new Exception("Entre em um canal de voz antes de ligar a câmera.");if(call.CameraOn){await call.StopCamera();RemoveLocal("camera");return;}
        var devices=await DeviceVideoTrackSource.GetCaptureDevicesAsync();if(devices.Count==0)throw new Exception("Nenhuma câmera encontrada. Verifique o dispositivo e a permissão de câmera do Windows.");
        var dialog=new Window{Title="Ligar câmera",Width=460,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);var choices=new ComboBox{ItemsSource=devices.Select(d=>d.name).ToArray(),SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};var start=Ui.Action("Ligar e transmitir",true);start.Click+=(_,_)=>dialog.Close(choices.SelectedIndex);dialog.Content=new Border{Padding=new Thickness(26),Child=Ui.Stack(18,Ui.Label("Mostre seu melhor ângulo",23),Ui.Label("Sua câmera será transmitida às pessoas deste canal.",14,Ui.Muted),choices,Ui.Label("640 × 480 · 30 FPS",12,Ui.Muted),start)};
        var choice=await dialog.ShowDialog<int?>(this);if(choice!=null)await call.StartCamera(devices[choice.Value].id);
    }
    async Task ToggleShare()
    {
        if(!call!.InCall)throw new Exception("Entre em um canal de voz antes de compartilhar a tela.");if(call.Sharing){await call.StopShare();RemoveLocal("screen");return;}
        var targets=DesktopCapture.Targets();var dialog=new Window{Title="Compartilhar tela",Width=570,Height=430,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);
        var choices=new ComboBox{Name="ScreenShareSource",ItemsSource=targets,SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};var quality=new ComboBox{Name="ScreenShareQuality",ItemsSource=ScreenShareQuality.Presets,SelectedIndex=1,HorizontalAlignment=HorizontalAlignment.Stretch};var start=Ui.Action("Compartilhar agora",true);start.Name="StartScreenShare";start.Click+=(_,_)=>{if(choices.SelectedItem is ShareTarget target)dialog.Close((target,(ScreenShareQuality)quality.SelectedItem!));};
        dialog.Content=new Border{Padding=new Thickness(28),Child=Ui.Stack(20,Ui.Label("Compartilhe o que está rolando",24),Ui.Label("Escolha um monitor ou uma janela. Todos na chamada poderão assistir.",14,Ui.Muted),choices,quality,Ui.Label("Nesta versão, o compartilhamento transmite imagem. O áudio do computador não é transmitido. Janelas protegidas ou minimizadas podem exigir compartilhar o monitor.",12,Ui.Muted),start)};
        var result=await dialog.ShowDialog<(ShareTarget Target,ScreenShareQuality Quality)?>(this);if(result!=null)await call.StartShare(result.Value.Target,result.Value.Quality.Width,result.Value.Quality.Fps);
    }
    void UpdateCallControls()
    {
        UpdateCompactCall();if(call==null||micButton==null)return;micButton.Background=call.Muted?Ui.Brush("#48212A"):Avalonia.Media.Brushes.Transparent;ToolTip.SetTip(micButton,(call.InCall?(call.Muted?"Ativar microfone":"Silenciar microfone"):"Microfone")+" · clique direito para testar e configurar");
        micButton.IsEnabled=true;deafenButton!.IsEnabled=call.InCall;
        deafenButton!.Background=call.Deafened?Ui.Brush("#48212A"):Avalonia.Media.Brushes.Transparent;cameraButton!.Background=call.CameraOn?Ui.Brush("#253D32"):Avalonia.Media.Brushes.Transparent;shareButton!.Background=call.Sharing?Ui.Brush("#253D32"):Avalonia.Media.Brushes.Transparent;ToolTip.SetTip(cameraButton,call.CameraOn?"Desligar câmera":"Ligar câmera");ToolTip.SetTip(shareButton,call.Sharing?"Parar transmissão":"Compartilhar tela");
        cameraButton.IsEnabled=shareButton.IsEnabled=leaveButton!.IsEnabled=call.InCall;
        if(!call.InCall){callLabel.Text="Nenhuma chamada ativa";callLabel.Foreground=Ui.Muted;ClearVideos();voiceChannel=null;}else callLabel.Foreground=Ui.Green;
    }
    CallRoster currentRoster=new(0,[]);
    void RenderRoster(CallRoster roster)
    {
        currentRoster=roster;UpdateCompactCall();if(roster.ChannelId==0){ClearVideos();return;}
        callMembers.Children.Clear();foreach(var peer in roster.Peers){var person=CallAvatar(new(peer.UserId,peer.Name,peer.Avatar),25);ToolTip.SetTip(person,peer.Name+(peer.ConnectionId==connection.ConnectionId?" (você)":""));person.Margin=new Thickness(2);callMembers.Children.Add(person);}
        callLabel.Text=$"● Na chamada · {roster.Peers.Length} participante{(roster.Peers.Length==1?"":"s")}";
        var ids=roster.Peers.Select(p=>p.ConnectionId).ToHashSet();activeScreens.RemoveWhere(id=>!ids.Contains(id));UpdateCompactCall();foreach(var key in videoTiles.Keys.Where(k=>!ids.Contains(k.Split(':')[0])).ToArray()){var tile=videoTiles[key];DetachVideo(tile);tile.Dispose();videoTiles.Remove(key);}
        foreach(var (key,tile) in videoTiles){var parts=key.Split(':');var peer=roster.Peers.FirstOrDefault(p=>p.ConnectionId==parts[0]);if(peer!=null)tile.SetTitle(peer.Name+(parts[1]=="screen"?" · Tela":" · Câmera"));}
    }
    void RemoveLocal(string kind){var key=connection.ConnectionId+":"+kind;suppressedVideo.Add(key);if(videoTiles.Remove(key,out var tile)){DetachVideo(tile);tile.Dispose();}}
    static void DetachVideo(VideoTile tile){if(tile.Root.Parent is Panel panel)panel.Children.Remove(tile.Root);}
    void ClearVideos(){foreach(var tile in videoTiles.Values)tile.Dispose();videoTiles.Clear();videoGrid.Children.Clear();activeScreens.Clear();callMembers.Children.Clear();suppressedVideo.Clear();frameQueue.Clear();}
    void RenderFrames()
    {
        if(call?.InCall!=true)return;
        foreach(var (key,pixels) in frameQueue.Drain()){
            if(suppressedVideo.Contains(key))continue;
            if(!videoTiles.TryGetValue(key,out var tile)){
                var parts=key.Split(':');var peer=currentRoster.Peers.FirstOrDefault(p=>p.ConnectionId==parts[0]);if(peer==null)continue;
                tile=new VideoTile(peer.Name+(parts[1]=="screen"?" · Tela":" · Câmera"));videoTiles[key]=tile;if(parts[1]=="screen"){activeScreens.Add(parts[0]);UpdateCompactCall();}else videoGrid.Children.Add(tile.Root);
            }
            tile.Update(pixels);
        }
    }
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}


