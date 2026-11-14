using System.Security.Cryptography;
namespace JanjaCord.Server.Authentication;
public static class Passwords
{
    public static string Hash(string password)
    { var salt=RandomNumberGenerator.GetBytes(16); return Convert.ToBase64String(salt)+":"+Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password,salt,210000,HashAlgorithmName.SHA256,32)); }
    public static bool Verify(string password,string stored)
    { var parts=stored.Split(':'); return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(parts[1]),Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(parts[0]),210000,HashAlgorithmName.SHA256,32)); }
}
