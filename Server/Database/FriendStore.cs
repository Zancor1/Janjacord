using JanjaCord.Shared;
namespace JanjaCord.Server.Database;
public sealed class FriendStore(Store store,Networking.PresenceDirectory presence,Networking.CallDirectory calls)
{
    public List<FriendEntry> List(long user)
    {
        using var db=store.Open();using var cmd=Store.Command(db,"SELECT u.id,u.name,p.avatar,f.state,f.requestedBy FROM friendships f JOIN users u ON u.id=CASE WHEN f.lowId=$u THEN f.highId ELSE f.lowId END LEFT JOIN profiles p ON p.userId=u.id WHERE f.lowId=$u OR f.highId=$u ORDER BY u.name",("$u",user));using var rows=cmd.ExecuteReader();var result=new List<FriendEntry>();
        while(rows.Read())result.Add(new(rows.GetInt64(0),rows.GetString(1),rows.IsDBNull(2)?null:rows.GetString(2),rows.GetString(3),rows.GetInt64(4)!=user,presence.Online(rows.GetInt64(0)),calls.UserBusy(rows.GetInt64(0))));return result;
    }
    public bool Related(long user,long peer,bool acceptedOnly=false)
    {using var db=store.Open();using var cmd=Store.Command(db,"SELECT state FROM friendships WHERE lowId=$l AND highId=$h",("$l",Math.Min(user,peer)),("$h",Math.Max(user,peer)));return cmd.ExecuteScalar() is string state&&(!acceptedOnly||state=="accepted");}
    public DirectMessage Send(long user,SendDirectMessage input)
    {
        if(string.IsNullOrWhiteSpace(input.Text)||input.Text.Length>4000||!Guid.TryParse(input.ClientId,out _)||!Related(user,input.PeerId,true))throw new InvalidOperationException("Aceite a amizade antes de enviar mensagens. Limite: 4000 caracteres.");
        using var db=store.Open();using var tx=db.BeginTransaction();
        using(var insert=Store.Command(db,"INSERT OR IGNORE INTO direct_messages(senderId,recipientId,body,sentAt,clientId) VALUES($u,$p,$b,$t,$c)",("$u",user),("$p",input.PeerId),("$b",input.Text),("$t",DateTimeOffset.UtcNow.ToString("O")),("$c",input.ClientId))){insert.Transaction=tx;insert.ExecuteNonQuery();}
        using var cmd=Store.Command(db,"SELECT m.id,m.recipientId,u.name,m.body,m.sentAt FROM direct_messages m JOIN users u ON u.id=m.senderId WHERE m.senderId=$u AND m.clientId=$c",("$u",user),("$c",input.ClientId));cmd.Transaction=tx;
        DirectMessage result;using(var row=cmd.ExecuteReader()){row.Read();if(row.GetInt64(1)!=input.PeerId)throw new InvalidOperationException("Identificador de mensagem já utilizado em outra conversa.");result=new(row.GetInt64(0),user,input.PeerId,row.GetString(2),row.GetString(3),DateTimeOffset.Parse(row.GetString(4)),input.ClientId);}tx.Commit();return result;
    }
    public List<DirectMessage> History(long user,long peer,long? before)
    {
        using var db=store.Open();using var cmd=Store.Command(db,"SELECT m.id,m.senderId,m.recipientId,u.name,m.body,m.sentAt,m.clientId FROM direct_messages m JOIN users u ON u.id=m.senderId WHERE ((senderId=$u AND recipientId=$p) OR (senderId=$p AND recipientId=$u)) AND m.id<$b ORDER BY m.id DESC LIMIT 100",("$u",user),("$p",peer),("$b",before??long.MaxValue));using var row=cmd.ExecuteReader();var result=new List<DirectMessage>();while(row.Read())result.Add(new(row.GetInt64(0),row.GetInt64(1),row.GetInt64(2),row.GetString(3),row.GetString(4),DateTimeOffset.Parse(row.GetString(5)),row.GetString(6)));result.Reverse();return result;
    }
}
