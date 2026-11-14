using JanjaCord.Shared;
namespace JanjaCord.Server.Database;
public sealed class DirectGroupStore(Store store,FriendStore friends)
{
    public const long RoomBase=-1_000_000_000_000;
    public static long Room(long group)=>RoomBase-group;
    public static bool IsRoom(long room)=>room<RoomBase;
    public bool Member(long user,long group){using var db=store.Open();using var cmd=Store.Command(db,"SELECT 1 FROM dm_group_members WHERE groupId=$g AND userId=$u",("$g",group),("$u",user));return cmd.ExecuteScalar()!=null;}
    public long[] Members(long group){using var db=store.Open();using var cmd=Store.Command(db,"SELECT userId FROM dm_group_members WHERE groupId=$g",("$g",group));using var row=cmd.ExecuteReader();var ids=new List<long>();while(row.Read())ids.Add(row.GetInt64(0));return ids.ToArray();}
    public DirectGroup Get(long user,long group)
    {
        if(!Member(user,group))throw new InvalidOperationException("Você não participa deste grupo.");
        using var db=store.Open();string name;long owner;
        using(var cmd=Store.Command(db,"SELECT name,ownerId FROM dm_groups WHERE id=$g",("$g",group))){using var row=cmd.ExecuteReader();if(!row.Read())throw new InvalidOperationException("Grupo não encontrado.");name=row.GetString(0);owner=row.GetInt64(1);}
        using var read=Store.Command(db,"SELECT u.id,u.name,p.avatar FROM dm_group_members m JOIN users u ON u.id=m.userId LEFT JOIN profiles p ON p.userId=u.id WHERE m.groupId=$g ORDER BY u.name",("$g",group));using var rows=read.ExecuteReader();var members=new List<MemberProfile>();while(rows.Read())members.Add(new(rows.GetInt64(0),rows.GetString(1),rows.IsDBNull(2)?null:rows.GetString(2)));
        return new(group,name,owner,members.ToArray());
    }
    public DirectGroup[] List(long user){using var db=store.Open();using var cmd=Store.Command(db,"SELECT groupId FROM dm_group_members WHERE userId=$u ORDER BY groupId DESC",("$u",user));using var row=cmd.ExecuteReader();var ids=new List<long>();while(row.Read())ids.Add(row.GetInt64(0));return ids.Select(id=>Get(user,id)).ToArray();}
    public DirectGroup Create(long user,CreateDirectGroup input)
    {
        var people=(input.Members??[]).Append(user).Distinct().ToArray();var name=input.Name?.Trim()??"";
        if(people.Length<2||people.Length>10||name.Length>80)throw new InvalidOperationException("Escolha de2 a10 pessoas, incluindo você. Nome: até80 caracteres.");
        if(people.Any(id=>id!=user&&!friends.Related(user,id,true)))throw new InvalidOperationException("Só é possível adicionar amigos com amizade aceita.");
        if(name.Length==0)name="Novo grupo";
        using var db=store.Open();using var tx=db.BeginTransaction();long group;
        using(var cmd=Store.Command(db,"INSERT INTO dm_groups(name,ownerId) VALUES($n,$u); SELECT last_insert_rowid();",("$n",name),("$u",user))){cmd.Transaction=tx;group=(long)cmd.ExecuteScalar()!;}
        foreach(var id in people){using var cmd=Store.Command(db,"INSERT INTO dm_group_members VALUES($g,$u)",("$g",group),("$u",id));cmd.Transaction=tx;cmd.ExecuteNonQuery();}tx.Commit();return Get(user,group);
    }
    public GroupMessage Send(long user,SendGroupMessage input)
    {
        if(!Member(user,input.GroupId)||string.IsNullOrWhiteSpace(input.Text)||input.Text.Length>4000||!Guid.TryParse(input.ClientId,out _))throw new InvalidOperationException("Mensagem inválida ou acesso negado.");
        using var db=store.Open();using var tx=db.BeginTransaction();
        using(var cmd=Store.Command(db,"INSERT OR IGNORE INTO dm_group_messages(groupId,senderId,body,sentAt,clientId) VALUES($g,$u,$b,$t,$c)",("$g",input.GroupId),("$u",user),("$b",input.Text),("$t",DateTimeOffset.UtcNow.ToString("O")),("$c",input.ClientId))){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        GroupMessage result;using(var cmd=Store.Command(db,"SELECT m.id,m.groupId,u.name,m.body,m.sentAt FROM dm_group_messages m JOIN users u ON u.id=m.senderId WHERE m.senderId=$u AND m.clientId=$c",("$u",user),("$c",input.ClientId))){cmd.Transaction=tx;using var row=cmd.ExecuteReader();row.Read();if(row.GetInt64(1)!=input.GroupId)throw new InvalidOperationException("Identificador já usado em outro grupo.");result=new(row.GetInt64(0),input.GroupId,user,row.GetString(2),row.GetString(3),DateTimeOffset.Parse(row.GetString(4)),input.ClientId);}tx.Commit();return result;
    }
    public GroupMessage[] History(long user,long group,long? before)
    {
        if(!Member(user,group))throw new InvalidOperationException("Sem acesso ao grupo.");using var db=store.Open();using var cmd=Store.Command(db,"SELECT m.id,m.senderId,u.name,m.body,m.sentAt,m.clientId FROM dm_group_messages m JOIN users u ON u.id=m.senderId WHERE groupId=$g AND m.id<$b ORDER BY m.id DESC LIMIT 100",("$g",group),("$b",before??long.MaxValue));using var row=cmd.ExecuteReader();var messages=new List<GroupMessage>();while(row.Read())messages.Add(new(row.GetInt64(0),group,row.GetInt64(1),row.GetString(2),row.GetString(3),DateTimeOffset.Parse(row.GetString(4)),row.GetString(5)));messages.Reverse();return messages.ToArray();
    }
}
