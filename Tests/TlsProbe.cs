using JanjaCord.Client.Services;
using JanjaCord.Shared;
public static class TlsProbe
{
    public static async Task Run(string address,string pin)
    {
        var suffix=Guid.NewGuid().ToString("N")[..10];
        await using(var bad=new Connection()) {
            try{await bad.Login(address,"invalidpin","Password-test-2026",true,new string('0',64));throw new Exception("Certificado incorreto aceito.");}
            catch(HttpRequestException){Console.WriteLine("PASS: impressão incorreta recusa certificado HTTPS");}
        }
        await using var a=new Connection();await using var b=new Connection();
        await a.Login(address,"tlsa"+suffix,"Password-test-2026",true,pin);
        await b.Login(address,"tlsb"+suffix,"Password-test-2026",true,pin);
        await a.Post("/api/communities",new CreateCommunity("TLS "+suffix));
        var community=(await a.Get<List<Community>>("/api/communities")).Single();
        await a.Post($"/api/communities/{community.Id}/channels",new CreateChannel("tls","text"));
        var channel=(await a.Get<List<Channel>>($"/api/communities/{community.Id}/channels")).Single();
        await b.Post($"/api/communities/{community.Id}/join",new{});
        var received=new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        b.Message+=message=>received.TrySetResult(message);
        await a.Watch(channel.Id);await b.Watch(channel.Id);
        await a.Send(channel.Id,"HTTPS real "+suffix);
        var message=await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if(message.UserId!=a.Session!.UserId||!message.Text.EndsWith(suffix))throw new Exception("Mensagem TLS incorreta.");
        Console.WriteLine("PASS: serviço real do cliente autentica e troca mensagens via HTTPS/WSS com pin correto");
    }
}
