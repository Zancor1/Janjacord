using System.Security.Cryptography;
using JanjaCord.Server.Database;
using JanjaCord.Shared;
using Microsoft.Data.Sqlite;
namespace JanjaCord.Server.Authentication;
public static class DeviceAccounts
{
    public static IResult SignIn(DeviceIdentity input,Store store)
    {
        if(!NameRules.Valid(input.Name)||input.DeviceKey?.Length!=64||!input.DeviceKey.All(Uri.IsHexDigit))return Results.BadRequest("Use um nome de 2 a 32 caracteres, com letras, números, espaço, ponto, hífen ou sublinhado.");
        input=input with{Name=NameRules.Normalize(input.Name)};
        if(input.Avatar is {Length:>180000})return Results.BadRequest("Foto muito grande.");
        if(input.Avatar!=null) {
            try {var bytes=Convert.FromBase64String(input.Avatar);if(bytes.Length<24||bytes.Length>135000||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)) is <1 or >512||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4)) is <1 or >512)return Results.BadRequest("Use uma foto PNG válida de até 512 × 512.");}
            catch(FormatException){return Results.BadRequest("Foto inválida.");}
        }
        using var db=store.Open();using var transaction=db.BeginTransaction();
        var keyHash=Store.TokenHash(input.DeviceKey);
        using var find=Store.Command(db,"SELECT userId FROM device_keys WHERE keyHash=$k",("$k",keyHash));find.Transaction=transaction;
        long id;
        if(find.ExecuteScalar() is long existing){
            id=existing;
            using var taken=Store.Command(db,"SELECT 1 FROM user_names WHERE nameKey=$n AND userId<>$u",("$n",NameRules.Key(input.Name)),("$u",id));taken.Transaction=transaction;
            if(taken.ExecuteScalar()!=null)return Results.Conflict("Esse nome já está em uso. Escolha outro nome para seu perfil.");
            using var rename=Store.Command(db,"UPDATE users SET name=$n WHERE id=$u",("$n",input.Name.Trim()),("$u",id));rename.Transaction=transaction;rename.ExecuteNonQuery();
        }
        else {
            var chosen=input.Name;
            using(var check=Store.Command(db,"SELECT 1 FROM user_names WHERE nameKey=$n",("$n",NameRules.Key(chosen)))){check.Transaction=transaction;if(check.ExecuteScalar()!=null)return Results.Conflict("Esse nome já está em uso nesta rede. Escolha outro nome.");}
            using var create=Store.Command(db,"INSERT INTO users(name,hash) VALUES($n,$h); SELECT last_insert_rowid();",("$n",chosen),("$h",Passwords.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))));create.Transaction=transaction;id=(long)create.ExecuteScalar()!;
            using var device=Store.Command(db,"INSERT INTO device_keys VALUES($k,$u)",("$k",keyHash),("$u",id));device.Transaction=transaction;device.ExecuteNonQuery();
        }
        using var reserve=Store.Command(db,"INSERT INTO user_names VALUES($u,$n) ON CONFLICT(userId) DO UPDATE SET nameKey=$n",("$u",id),("$n",NameRules.Key(input.Name)));reserve.Transaction=transaction;reserve.ExecuteNonQuery();
        using var photo=Store.Command(db,"INSERT INTO profiles(userId,avatar) VALUES($u,$a) ON CONFLICT(userId) DO UPDATE SET avatar=$a",("$u",id),("$a",input.Avatar));photo.Transaction=transaction;photo.ExecuteNonQuery();
        using var readName=Store.Command(db,"SELECT name FROM users WHERE id=$u",("$u",id));readName.Transaction=transaction;var actualName=(string)readName.ExecuteScalar()!;
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var session=Store.Command(db,"INSERT INTO sessions VALUES($t,$u,$e)",("$t",Store.TokenHash(token)),("$u",id),("$e",DateTimeOffset.UtcNow.AddDays(1).ToString("O")));session.Transaction=transaction;session.ExecuteNonQuery();transaction.Commit();
        return Results.Ok(new Session(token,id,actualName));
    }
}
