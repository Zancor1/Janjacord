using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using JanjaCord.Shared;

if(args.FirstOrDefault()=="--screen-fps"){await ScreenFrameRateProbe.Run(!args.Contains("baseline"));return;}
if(args.FirstOrDefault()=="--tray-message"){var f=System.Reflection.Assembly.Load("Avalonia.Win32").GetTypes().SelectMany(t=>t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)).First(f=>f.Name=="WM_TRAYMOUSE");Console.WriteLine(f.GetRawConstantValue());return;}
if(args.FirstOrDefault()=="--groups"){await GroupsProbe.Run(args[1]);return;}
if(args.FirstOrDefault()=="--tls"){await TlsProbe.Run(args[1],args[2]);return;}
if(args.FirstOrDefault()=="--media"){await MediaProbe.Run(args[1],args[2]);return;}
if(args.FirstOrDefault()=="--auto"){await AutomaticProbe.Run();return;}
if(args.FirstOrDefault()=="--audio"){await SelectableAudioProbe.Run();return;}
if(args.FirstOrDefault()=="--playback-outbox"){PlaybackAndOutboxProbe.Run();return;}
if(args.FirstOrDefault()=="--voice-level"){VoiceLevelProbe.Run();PlaybackAndOutboxProbe.Run();return;}
if(args.FirstOrDefault()=="--ringtone"){await RingtoneProbe.Run(args[1]);return;}
if(args.FirstOrDefault()=="--noise"){NoiseSuppressionProbe.Run(args[1],args.Contains("--advanced"));return;}
var address=args.FirstOrDefault()??"http://localhost:5199";
var suffix=Guid.NewGuid().ToString("N")[..10];
using var a=new HttpClient{BaseAddress=new Uri(address)};
using var b=new HttpClient{BaseAddress=new Uri(address)};
async Task<Session> Register(HttpClient client,string name){using var response=await client.PostAsJsonAsync("/api/auth/register",new Credentials(name,"Test-password-2941"));response.EnsureSuccessStatusCode();var session=(await response.Content.ReadFromJsonAsync<Session>())!;client.DefaultRequestHeaders.Authorization=new("Bearer",session.Token);return session;}
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
var sa=await Register(a,"alice"+suffix);var sb=await Register(b,"bob"+suffix);
using(var unauthorized=new HttpClient{BaseAddress=new Uri(address)})Check((await unauthorized.GetAsync("/api/communities")).StatusCode==HttpStatusCode.Unauthorized,"rota protegida exige sessão");
using var created=await a.PostAsJsonAsync("/api/communities",new CreateCommunity("Integration "+suffix));created.EnsureSuccessStatusCode();var community=(await created.Content.ReadFromJsonAsync<Community>())!;
using var channelResponse=await a.PostAsJsonAsync($"/api/communities/{community.Id}/channels",new CreateChannel("geral","text"));channelResponse.EnsureSuccessStatusCode();var channel=(await channelResponse.Content.ReadFromJsonAsync<Channel>())!;
Check((await b.GetAsync($"/api/channels/{channel.Id}/messages")).StatusCode==HttpStatusCode.Forbidden,"não membro não lê histórico");
(await b.PostAsJsonAsync($"/api/communities/{community.Id}/join",new{})).EnsureSuccessStatusCode();
Check((await b.PostAsJsonAsync($"/api/communities/{community.Id}/channels",new CreateChannel("negado","text"))).StatusCode==HttpStatusCode.Forbidden,"membro não proprietário não cria canal");
HubConnection Hub(Session session)=>new HubConnectionBuilder().WithUrl(address+"/chat",o=>o.AccessTokenProvider=()=>Task.FromResult<string?>(session.Token)).Build();
await using var ha=Hub(sa);await using var hb=Hub(sb);
var received=new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
hb.On<ChatMessage>("Message",m=>received.TrySetResult(m));
await ha.StartAsync();await hb.StartAsync();await ha.InvokeAsync("Watch",channel.Id);await hb.InvokeAsync("Watch",channel.Id);
await ha.InvokeAsync("Send",new SendMessage(channel.Id,"Mensagem real "+suffix));
var message=await received.Task.WaitAsync(TimeSpan.FromSeconds(10));Check(message.UserId==sa.UserId&&message.Text.EndsWith(suffix),"mensagem SignalR entre dois clientes reais");
var history=await b.GetFromJsonAsync<List<ChatMessage>>($"/api/channels/{channel.Id}/messages");Check(history!.Any(m=>m.Id==message.Id),"mensagem persistida e recuperada por HTTP");
try{await hb.InvokeAsync("Send",new SendMessage(channel.Id,new string('x',4001))).WaitAsync(TimeSpan.FromSeconds(10));throw new Exception("Mensagem excedente aceita");}catch(Microsoft.AspNetCore.SignalR.HubException){Console.WriteLine("PASS: limite de mensagem");}
await DeletionProbe.Run(a,b,ha,hb,community.Id);
await FriendsProbe.Run(a,b,ha,hb,sa,sb);
await DirectCallProbe.Run(ha,hb,sa,sb);
await SelectableAudioProbe.Run();
PlaybackAndOutboxProbe.Run();
await ProfileProbe.Run(a,b,hb,sa,sb);
await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory,"persistence-check.json"),System.Text.Json.JsonSerializer.Serialize(new{ChannelId=channel.Id,MessageId=message.Id,Token=sb.Token}));
Console.WriteLine("Estado do teste de reinício salvo localmente; token não exibido.");
