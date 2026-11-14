using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR;
using JanjaCord.Shared;
namespace JanjaCord.Client.Services;
public sealed class Connection:IAsyncDisposable
{
    HttpClient http=new(){Timeout=TimeSpan.FromSeconds(15)};
    HubConnection? hub;
    public Session? Session {get;private set;}
    public string? ConnectionId=>hub?.ConnectionId;
    public bool Connected=>hub?.State==HubConnectionState.Connected;
    public void OfflineIdentity(long id,string name)=>Session=new("",id,name);
    void EnsureConnected(){if(!Connected)throw new Exception("Sua rede está offline. Aguarde a reconexão para realizar essa ação.");}
    public event Action<ChatMessage>? Message;
    public event Action<string>? Status;
    public event Action<CallRoster>? Roster;
    public event Action<RtcSignal>? SignalReceived;
    public event Action? Interrupted;
    public event Action<long>? CommunityDeleted;
    public event Action<long>? CallEnded;
    public event Action? FriendsChanged;
    public event Action<DirectMessage>? DirectMessageReceived;
    public event Action<CallActivity>? CallActivityChanged;
    public event Action<CallPresence>? CallPresenceChanged;
    public event Action? DirectGroupsChanged;
    public event Action<GroupMessage>? GroupMessageReceived;
    public event Action<GroupCallInvite>? GroupCallReceived;
    public Task<GroupMessage> SendGroup(long group,string text,string clientId)=>hub!.InvokeAsync<GroupMessage>("SendGroup",new SendGroupMessage(group,text,clientId));
    public Task StartGroupCall(long group)=>hub!.InvokeAsync("StartGroupCall",group);
    public event Action<MemberProfile>? ProfileChanged;
    public event Action<DirectCallInfo>? IncomingDirectCall,DirectCallReady,DirectCallEnded;
    readonly object pendingCallGate=new();
    readonly Queue<DirectCallInfo> pendingCalls=[];
    void ReceiveIncomingCall(DirectCallInfo info){Action<DirectCallInfo>? handler;lock(pendingCallGate){handler=IncomingDirectCall;if(handler==null){pendingCalls.Enqueue(info);return;}}handler(info);}
    public void DeliverPendingCalls(){DirectCallInfo[] pending;lock(pendingCallGate){pending=pendingCalls.ToArray();pendingCalls.Clear();}foreach(var info in pending)IncomingDirectCall?.Invoke(info);}
    public Task<DirectCallInfo> StartDirectCall(long peer)=>hub!.InvokeAsync<DirectCallInfo>("StartDirectCall",peer);
    public Task AcceptDirectCall(long id)=>hub!.InvokeAsync("AcceptDirectCall",id);
    public Task EndDirectCall(long id)=>hub!.InvokeAsync("EndDirectCall",id);
    DeviceIdentity? pendingIdentity;
    public async Task LoginDevice(string address,DeviceIdentity profile,string fingerprint)
    {
        pendingIdentity=profile;
        try{await Login(address,profile.Name,"",false,fingerprint);}finally{pendingIdentity=null;}
    }
    public async Task Login(string address,string name,string password,bool register,string fingerprint)
    {
        if(!Uri.TryCreate(address,UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new Exception("Use https://IP:porta e um certificado confiável para esse endereço.");
        var pin=fingerprint.Replace(" ","").Replace(":","").ToUpperInvariant();
        if(pin.Length!=64||!pin.All(Uri.IsHexDigit))throw new Exception("Copie a impressão SHA-256 de 64 caracteres exibida no console do servidor.");
        bool Verify(System.Security.Cryptography.X509Certificates.X509Certificate2? certificate)=>certificate!=null&&System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Convert.FromHexString(pin),certificate.GetCertHash(System.Security.Cryptography.HashAlgorithmName.SHA256));
        http.Dispose();
        http=new HttpClient(new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,cert,_,_)=>Verify(cert)}){Timeout=TimeSpan.FromSeconds(15),BaseAddress=uri};
        using(var health=await http.GetAsync("/health")){health.EnsureSuccessStatusCode();using var data=System.Text.Json.JsonDocument.Parse(await health.Content.ReadAsStringAsync());if(!data.RootElement.TryGetProperty("protocol",out var protocol)||protocol.GetInt32()!=6)throw new Exception("Atualize o cliente que hospeda a rede para JanjaCord 0.8. Todos precisam usar a mesma versão para conversar por voz.");}
        using var response=pendingIdentity!=null?await http.PostAsJsonAsync("/api/auth/device",pendingIdentity):await http.PostAsJsonAsync("/api/auth/"+(register?"register":"login"),new Credentials(name,password));
        if(!response.IsSuccessStatusCode)throw new Exception($"Autenticação: {(int)response.StatusCode}. {await response.Content.ReadAsStringAsync()}");
        Session=(await response.Content.ReadFromJsonAsync<Session>())!;
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Session.Token);
        hub=new HubConnectionBuilder().WithUrl(new Uri(uri,"/chat"),o=>{
            o.AccessTokenProvider=()=>Task.FromResult<string?>(Session.Token);
            o.HttpMessageHandlerFactory=handler=>{if(handler is HttpClientHandler clientHandler)clientHandler.ServerCertificateCustomValidationCallback=(_,cert,_,_)=>Verify(cert);return handler;};
            o.WebSocketConfiguration=ws=>ws.RemoteCertificateValidationCallback=(_,cert,_,_)=>cert!=null&&Verify(new System.Security.Cryptography.X509Certificates.X509Certificate2(cert));
        }).WithAutomaticReconnect().Build();
        hub.On<DirectCallInfo>("IncomingDirectCall",ReceiveIncomingCall);
        hub.On<CallActivity>("CallActivityChanged",a=>CallActivityChanged?.Invoke(a));
        hub.On<CallPresence>("CallPresenceChanged",p=>CallPresenceChanged?.Invoke(p));
        hub.On("DirectGroupsChanged",()=>DirectGroupsChanged?.Invoke());
        hub.On<GroupMessage>("GroupMessage",m=>GroupMessageReceived?.Invoke(m));
        hub.On<GroupCallInvite>("GroupCallInvite",m=>GroupCallReceived?.Invoke(m));
        hub.On<DirectCallInfo>("DirectCallReady",m=>DirectCallReady?.Invoke(m));
        hub.On<DirectCallInfo>("DirectCallEnded",m=>DirectCallEnded?.Invoke(m));
        hub.On<ChatMessage>("Message",m=>Message?.Invoke(m));
        hub.On<CallRoster>("CallRoster",r=>Roster?.Invoke(r));
        hub.On<RtcSignal>("RtcSignal",s=>SignalReceived?.Invoke(s));
        hub.On<long>("CommunityDeleted",id=>CommunityDeleted?.Invoke(id));
        hub.On<long>("CallEnded",id=>CallEnded?.Invoke(id));
        hub.On("FriendsChanged",()=>FriendsChanged?.Invoke());
        hub.On<DirectMessage>("DirectMessage",message=>DirectMessageReceived?.Invoke(message));
        hub.On<MemberProfile>("ProfileChanged",p=>{if(Session is {} current&&p.Id==current.UserId)Session=current with{Name=p.Name};ProfileChanged?.Invoke(p);});
        hub.Reconnecting+=_=>{Interrupted?.Invoke();Status?.Invoke("Reconectando… entre novamente na chamada após reconectar.");return Task.CompletedTask;};
        hub.Reconnected+=async _=>{if(channel!=null)try{await hub.InvokeAsync("Watch",channel.Value);}catch(HubException){channel=null;}FriendsChanged?.Invoke();Status?.Invoke("Conectado");};
        hub.Closed+=_=>{Interrupted?.Invoke();Status?.Invoke("Conexão encerrada. Reabra o cliente para conectar novamente.");return Task.CompletedTask;};
        await hub.StartAsync();
    }
    long? channel;
    public async Task Watch(long id){if(channel!=null)await hub!.InvokeAsync("Unwatch",channel.Value);await hub!.InvokeAsync("Watch",id);channel=id;}
    public Task Send(long id,string text)=>hub!.InvokeAsync("Send",new SendMessage(id,text));
    public Task<DirectMessage> SendDirect(long peer,string text,string? clientId=null)=>hub!.InvokeAsync<DirectMessage>("SendDirect",new SendDirectMessage(peer,text,clientId??Guid.NewGuid().ToString()));
    public async Task<T> PostResult<T,TInput>(string path,TInput input){EnsureConnected();using var response=await http.PostAsJsonAsync(path,input);if(!response.IsSuccessStatusCode)throw new Exception(await response.Content.ReadAsStringAsync());return (await response.Content.ReadFromJsonAsync<T>())!;}
    public Task<CallRoster> JoinCall(long id)=>hub!.InvokeAsync<CallRoster>("JoinCall",id);
    public Task LeaveCall()=>hub?.State==HubConnectionState.Connected?hub.InvokeAsync("LeaveCall"):Task.CompletedTask;
    public Task Signal(string peer,string kind,string payload)=>hub!.InvokeAsync("Signal",new SendRtcSignal(peer,kind,payload));
    public async Task<T> Get<T>(string path){EnsureConnected();return await http.GetFromJsonAsync<T>(path)??throw new Exception("Resposta vazia.");}
    public async Task Post<T>(string path,T input){EnsureConnected();using var response=await http.PostAsJsonAsync(path,input);if(!response.IsSuccessStatusCode)throw new Exception($"Operação recusada: {(int)response.StatusCode}");}
    public async Task JoinCommunity(long id)
    {
        EnsureConnected();using var response=await http.PostAsJsonAsync($"/api/communities/{id}/join",new{});
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)throw new InvalidOperationException("Esse servidor não existe. Confira o ID e tente novamente.");
        if(!response.IsSuccessStatusCode)throw new Exception("Não foi possível entrar no servidor. Tente novamente.");
    }
    public async Task Delete(string path){EnsureConnected();using var response=await http.DeleteAsync(path);if(!response.IsSuccessStatusCode)throw new Exception($"Não foi possível excluir ({(int)response.StatusCode}). Somente o dono pode excluir o servidor.");}
    public async Task<MemberProfile> UpdateProfile(UpdateProfile input){EnsureConnected();using var response=await http.PutAsJsonAsync("/api/profile",input);if(!response.IsSuccessStatusCode){var detail=await response.Content.ReadAsStringAsync();throw new Exception(string.IsNullOrWhiteSpace(detail)?"Não foi possível salvar o perfil. Atualize também o cliente que hospeda a rede.":detail);}var result=(await response.Content.ReadFromJsonAsync<MemberProfile>())!;Session=Session! with{Name=result.Name};return result;}
    public async ValueTask DisposeAsync(){if(hub!=null)await hub.DisposeAsync();http.Dispose();}
}
