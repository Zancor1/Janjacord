using System.Security.Cryptography;
using System.Text.Json;
namespace JanjaCord.Client.Services;
public sealed class LocalProfile
{
    public string Name {get;set;}="";
    public bool NoiseSuppressionAdvanced {get;set;}=true;
    public bool NoiseSuppressionEnabled {get;set;}=true;
    public float NoiseSuppressionStrength {get;set;}=.9f;
    public string? InputDeviceId {get;set;}
    public string? OutputDeviceId {get;set;}
    public string? Avatar {get;set;}
    public bool PendingAvatarSync {get;set;}
    public System.Collections.Concurrent.ConcurrentDictionary<string,int> PeerVolumes {get;set;}=new();
    public string Address {get;set;}="https://localhost:5000";
    public string Fingerprint {get;set;}="";
    public string ProtectedKey {get;set;}="";
    public long UserId {get;set;}
    public int NetworkProtocol {get;set;}
    public static string DirectoryPath=>Environment.GetEnvironmentVariable("JANJACORD_PROFILE_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JanjaCord");
    static string FilePath=>Path.Combine(DirectoryPath,"profile.json");
    public static LocalProfile Load()
    {foreach(var path in new[]{FilePath,FilePath+".bak"}){try{if(File.Exists(path)&&JsonSerializer.Deserialize<LocalProfile>(File.ReadAllText(path)) is LocalProfile saved)return saved;}catch(IOException){}catch(JsonException){}catch(UnauthorizedAccessException){}}return new();}
    public string DeviceKey()
    {
        if(ProtectedKey.Length>0){try{return Convert.ToHexString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedKey),null,DataProtectionScope.CurrentUser));}catch(CryptographicException){throw new Exception("Este perfil pertence a outro usuário Windows. Crie um novo perfil neste computador.");}}
        var secret=RandomNumberGenerator.GetBytes(32);ProtectedKey=Convert.ToBase64String(ProtectedData.Protect(secret,null,DataProtectionScope.CurrentUser));Save();return Convert.ToHexString(secret);
    }
    public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);var temporary=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(this));if(File.Exists(FilePath))File.Copy(FilePath,FilePath+".bak",true);File.Move(temporary,FilePath,true);}
}
