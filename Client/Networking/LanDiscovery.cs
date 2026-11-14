using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using JanjaCord.Shared;
namespace JanjaCord.Client.Networking;
public sealed record DiscoveredHost(string Address,HostAdvertisement Host);
public static class LanDiscovery
{
    public static async Task<List<DiscoveredHost>> Find(TimeSpan duration,CancellationToken cancellation=default)
    {
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(cancellation);stop.CancelAfter(duration);
        using var udp=new UdpClient(AddressFamily.InterNetwork){EnableBroadcast=true};udp.Client.Bind(new IPEndPoint(IPAddress.Any,0));
        var nonce=Guid.NewGuid().ToString("N");var packet=Encoding.ASCII.GetBytes("JANJACORD_DISCOVER_V6:"+nonce);
        var addresses=new HashSet<IPAddress>{IPAddress.Loopback,IPAddress.Broadcast};
        foreach(var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up))foreach(var unicast in nic.GetIPProperties().UnicastAddresses.Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork)){
            var ip=unicast.Address.GetAddressBytes();var mask=unicast.IPv4Mask.GetAddressBytes();addresses.Add(new IPAddress(ip.Zip(mask,(a,m)=>(byte)(a|~m)).ToArray()));
        }
        foreach(var address in addresses)try{await udp.SendAsync(packet,new IPEndPoint(address,39334),stop.Token);}catch(SocketException){}
        var hosts=new Dictionary<string,DiscoveredHost>();
        try{while(true){UdpReceiveResult response;try{response=await udp.ReceiveAsync(stop.Token);}catch(SocketException ex)when(ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused){continue;}if(response.Buffer.Length>2048)continue;
            try{using var document=JsonDocument.Parse(response.Buffer);if(document.RootElement.GetProperty("Nonce").GetString()!=nonce)continue;var host=document.RootElement.GetProperty("Host").Deserialize<HostAdvertisement>();
                if(host is not {Protocol:6}||host.Port is <1 or >65535||host.Id is not {Length:32}||host.Fingerprint is not {Length:64}||!host.Fingerprint.All(Uri.IsHexDigit))continue;
                hosts[host.Id]=new($"https://{response.RemoteEndPoint.Address}:{host.Port}",host);
            }catch(JsonException){}catch(KeyNotFoundException){}
        }}catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){}
        return hosts.Values.OrderBy(h=>h.Host.Id,StringComparer.Ordinal).ToList();
    }
}
