using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using JanjaCord.Shared;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
namespace JanjaCord.Server.Networking;
public sealed record DiscoveryIdentity(string Id,string Fingerprint);
public sealed class DiscoveryService(IServer server,DiscoveryIdentity identity,ILogger<DiscoveryService> logger):BackgroundService
{
    public const int DiscoveryPort=39334;
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        try {
            using var udp=new UdpClient(AddressFamily.InterNetwork);
            udp.ExclusiveAddressUse=false;udp.Client.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);udp.Client.Bind(new IPEndPoint(IPAddress.Any,DiscoveryPort));
            while(!stop.IsCancellationRequested){
                UdpReceiveResult packet;try{packet=await udp.ReceiveAsync(stop);}catch(SocketException ex)when(ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused){continue;}if(packet.Buffer.Length>128)continue;
                var query=Encoding.ASCII.GetString(packet.Buffer);if(!query.StartsWith("JANJACORD_DISCOVER_V6:"))continue;
                var address=server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();if(address==null||!Uri.TryCreate(address,UriKind.Absolute,out var uri))continue;
                var ad=new HostAdvertisement(identity.Id,uri.Port,identity.Fingerprint,Environment.MachineName);
                var bytes=JsonSerializer.SerializeToUtf8Bytes(new {Nonce=query[22..],Host=ad});
                await udp.SendAsync(bytes,packet.RemoteEndPoint,stop);
            }
        }catch(OperationCanceledException){}catch(SocketException ex){logger.LogWarning("Descoberta LAN indisponível: {Error}",ex.Message);}
    }
}
