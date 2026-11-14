using System.Security.Cryptography;
using System.Text.Json;
using JanjaCord.Client.Services;
using JanjaCord.Server;
using JanjaCord.Server.Networking;
using JanjaCord.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
namespace JanjaCord.Client.Networking;
public sealed class NetworkOfflineException(string message):Exception(message);
public sealed class AutomaticNetwork:IAsyncDisposable
{
    WebApplication? host;
    FileStream? lease;
    public bool Hosting=>host!=null;
    public DiscoveredHost? Endpoint {get;private set;}
    string NetworkDirectory=>Path.Combine(Environment.GetEnvironmentVariable("JANJACORD_NETWORK_DIR")??LocalProfile.DirectoryPath,"network");
    string EndpointFile=>Path.Combine(NetworkDirectory,"endpoint.json");
    public async Task<DiscoveredHost> Connect(LocalProfile profile)
    {
        if(Endpoint!=null&&await Reachable(Endpoint))return Endpoint;
        Directory.CreateDirectory(NetworkDirectory);
        var local=await LocalEndpoint();if(local!=null&&(profile.NetworkProtocol<4||local.Host.Fingerprint==profile.Fingerprint))return Endpoint=local;
        if(profile.Fingerprint.Length==64&&Uri.TryCreate(profile.Address,UriKind.Absolute,out var previous)){
            var saved=new DiscoveredHost(profile.Address,new("saved",previous.Port,profile.Fingerprint,"Sua rede"));if(await Reachable(saved))return Endpoint=saved;
        }
        var isolated=Environment.GetEnvironmentVariable("JANJACORD_DISABLE_DISCOVERY")=="1";
        var found=isolated?new List<DiscoveredHost>():await LanDiscovery.Find(TimeSpan.FromMilliseconds(1400));foreach(var candidate in found)if((profile.NetworkProtocol<4||candidate.Host.Fingerprint==profile.Fingerprint)&&await Reachable(candidate))return Endpoint=candidate;
        if(profile.NetworkProtocol>=4){
            var ownCertificate=Path.Combine(LocalProfile.DirectoryPath,"host","server.pfx");var owns=false;
            if(File.Exists(ownCertificate)){using var certificate=ServerCertificate.Load(Path.GetDirectoryName(ownCertificate)!);owns=certificate.GetCertHashString(HashAlgorithmName.SHA256)==profile.Fingerprint;}
            if(!owns)throw new NetworkOfflineException("Sua rede está offline. Suas conversas salvas continuam aqui; o cliente que hospeda a rede precisa estar aberto.");
        }
        try{lease=new FileStream(Path.Combine(NetworkDirectory,"host.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){
            for(var i=0;i<30;i++){await Task.Delay(200);local=await LocalEndpoint();if(local!=null)return Endpoint=local;}
            throw new Exception("Outra janela está iniciando sua rede. Aguarde e tente novamente.");
        }
        try{
            var data=Path.Combine(LocalProfile.DirectoryPath,"host");Directory.CreateDirectory(data);ImportPrevious(data);
            var identityPath=Path.Combine(data,"host-id.txt");if(!File.Exists(identityPath))File.WriteAllText(identityPath,Guid.NewGuid().ToString("N"));
            host=await Task.Run(()=>ServerHost.Create(["--port","0","--data-dir",data,"--discover",isolated?"false":"true","--noninteractive"]));
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20)))await host.StartAsync(timeout.Token);
            using var certificate=ServerCertificate.Load(data);
            var port=new Uri(host.Urls.Single()).Port;
            var own=new DiscoveredHost($"https://127.0.0.1:{port}",new(File.ReadAllText(Path.Combine(data,"host-id.txt")).Trim(),port,certificate.GetCertHashString(HashAlgorithmName.SHA256),Environment.MachineName));
            File.WriteAllText(EndpointFile,JsonSerializer.Serialize(own));
            var others=isolated?new List<DiscoveredHost>():await LanDiscovery.Find(TimeSpan.FromMilliseconds(1200));var winner=others.Where(h=>string.CompareOrdinal(h.Host.Id,own.Host.Id)<0).FirstOrDefault();
            if(winner!=null&&await Reachable(winner)){await StopHost();return Endpoint=winner;}
            return Endpoint=own;
        }catch{await StopHost();throw;}
    }
    void ImportPrevious(string data)
    {
        var target=Path.Combine(data,"janjacord.db");if(File.Exists(target)||Environment.GetEnvironmentVariable("JANJACORD_DISABLE_IMPORT")=="1")return;
        var previous=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","Server","data","janjacord.db"));if(!File.Exists(previous))return;
        using var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=previous,Mode=SqliteOpenMode.ReadOnly}.ToString());source.Open();using var destination=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=target}.ToString());destination.Open();source.BackupDatabase(destination);
    }
    async Task<DiscoveredHost?> LocalEndpoint(){try{if(!File.Exists(EndpointFile))return null;var e=JsonSerializer.Deserialize<DiscoveredHost>(await File.ReadAllTextAsync(EndpointFile));return e!=null&&await Reachable(e)?e:null;}catch(IOException){return null;}catch(JsonException){return null;}}
    public static async Task<bool> Reachable(DiscoveredHost endpoint)
    {
        try{using var client=new HttpClient(new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,cert,_,_)=>cert!=null&&string.Equals(cert.GetCertHashString(HashAlgorithmName.SHA256),endpoint.Host.Fingerprint,StringComparison.OrdinalIgnoreCase)}){Timeout=TimeSpan.FromMilliseconds(1600)};
            using var health=await client.GetAsync(endpoint.Address+"/health");if(!health.IsSuccessStatusCode)return false;using var json=JsonDocument.Parse(await health.Content.ReadAsStringAsync());return json.RootElement.TryGetProperty("protocol",out var protocol)&&protocol.GetInt32()==6;
        }catch(HttpRequestException){return false;}catch(TaskCanceledException){return false;}catch(JsonException){return false;}
    }
    async Task StopHost(){if(host!=null){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));try{await host.StopAsync(timeout.Token);}catch(OperationCanceledException){}await host.DisposeAsync();host=null;}lease?.Dispose();lease=null;}
    public async ValueTask DisposeAsync()=>await StopHost();
}
