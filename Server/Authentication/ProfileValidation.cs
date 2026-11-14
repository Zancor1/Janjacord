using System.Buffers.Binary;
using JanjaCord.Shared;
namespace JanjaCord.Server.Authentication;
public static class ProfileValidation
{
    public static string? Error(string? name,string? avatar)
    {
        if(!NameRules.Valid(name))return "Use um nome de 2 a 32 caracteres, com letras, números, espaço, ponto, hífen ou sublinhado.";
        if(avatar==null)return null;if(avatar.Length>180000)return "Foto muito grande.";
        try{var bytes=Convert.FromBase64String(avatar);if(bytes.Length<24||bytes.Length>135000||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)) is <1 or >512||BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4)) is <1 or >512)return "Use uma foto PNG válida de até 512 × 512.";}
        catch(FormatException){return "Foto inválida.";}return null;
    }
}
