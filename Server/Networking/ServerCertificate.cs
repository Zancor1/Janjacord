using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace JanjaCord.Server.Networking;
public static class ServerCertificate
{
    public static X509Certificate2 Load(string directory)
    {
        var path=Path.Combine(directory,"server.pfx");
        if(!File.Exists(path)) {
            using var key=RSA.Create(3072);
            var request=new CertificateRequest("CN=JanjaCord local server",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
            var names=new SubjectAlternativeNameBuilder();names.AddDnsName("localhost");names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddYears(2));
            File.WriteAllBytes(path,certificate.Export(X509ContentType.Pfx));
        }
        return X509CertificateLoader.LoadPkcs12FromFile(path,null);
    }
}
