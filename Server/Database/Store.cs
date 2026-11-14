using Microsoft.Data.Sqlite;
namespace JanjaCord.Server.Database;
public sealed class Store
{
    readonly string connectionString;
    public Store(string path)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
        PRAGMA journal_mode=WAL;
        CREATE TABLE IF NOT EXISTS users(id INTEGER PRIMARY KEY,name TEXT NOT NULL UNIQUE COLLATE NOCASE,hash TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS sessions(token TEXT PRIMARY KEY,userId INTEGER NOT NULL REFERENCES users(id),expires TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS communities(id INTEGER PRIMARY KEY,name TEXT NOT NULL,ownerId INTEGER NOT NULL REFERENCES users(id));
        CREATE TABLE IF NOT EXISTS members(communityId INTEGER REFERENCES communities(id),userId INTEGER REFERENCES users(id),PRIMARY KEY(communityId,userId));
        CREATE TABLE IF NOT EXISTS channels(id INTEGER PRIMARY KEY,communityId INTEGER NOT NULL REFERENCES communities(id),name TEXT NOT NULL,kind TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS messages(id INTEGER PRIMARY KEY,channelId INTEGER NOT NULL REFERENCES channels(id),userId INTEGER NOT NULL REFERENCES users(id),body TEXT NOT NULL,sentAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS device_keys(keyHash TEXT PRIMARY KEY,userId INTEGER NOT NULL REFERENCES users(id));
        CREATE TABLE IF NOT EXISTS profiles(userId INTEGER PRIMARY KEY REFERENCES users(id),avatar TEXT);
        CREATE TABLE IF NOT EXISTS ids(name TEXT PRIMARY KEY,value INTEGER NOT NULL);
        INSERT OR IGNORE INTO ids SELECT 'community',COALESCE(MAX(id),0) FROM communities;
        INSERT OR IGNORE INTO ids SELECT 'channel',COALESCE(MAX(id),0) FROM channels;
        CREATE TABLE IF NOT EXISTS user_names(userId INTEGER PRIMARY KEY REFERENCES users(id),nameKey TEXT NOT NULL UNIQUE);
        CREATE TABLE IF NOT EXISTS friendships(lowId INTEGER NOT NULL REFERENCES users(id),highId INTEGER NOT NULL REFERENCES users(id),requestedBy INTEGER NOT NULL REFERENCES users(id),state TEXT NOT NULL,createdAt TEXT NOT NULL,PRIMARY KEY(lowId,highId),CHECK(lowId<highId));
        CREATE TABLE IF NOT EXISTS direct_messages(id INTEGER PRIMARY KEY AUTOINCREMENT,senderId INTEGER NOT NULL REFERENCES users(id),recipientId INTEGER NOT NULL REFERENCES users(id),body TEXT NOT NULL,sentAt TEXT NOT NULL,clientId TEXT NOT NULL,UNIQUE(senderId,clientId));
        CREATE TABLE IF NOT EXISTS dm_groups(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,ownerId INTEGER NOT NULL REFERENCES users(id));
        CREATE TABLE IF NOT EXISTS dm_group_members(groupId INTEGER NOT NULL REFERENCES dm_groups(id),userId INTEGER NOT NULL REFERENCES users(id),PRIMARY KEY(groupId,userId));
        CREATE TABLE IF NOT EXISTS dm_group_messages(id INTEGER PRIMARY KEY AUTOINCREMENT,groupId INTEGER NOT NULL REFERENCES dm_groups(id),senderId INTEGER NOT NULL REFERENCES users(id),body TEXT NOT NULL,sentAt TEXT NOT NULL,clientId TEXT NOT NULL,UNIQUE(senderId,clientId));
        CREATE INDEX IF NOT EXISTS dm_group_history ON dm_group_messages(groupId,id);
        CREATE INDEX IF NOT EXISTS direct_pair ON direct_messages(senderId,recipientId,id);
        """;
        cmd.ExecuteNonQuery();
        var names=new List<(long,string)>();using(var read=Command(db,"SELECT id,name FROM users")){using var rows=read.ExecuteReader();while(rows.Read())names.Add((rows.GetInt64(0),rows.GetString(1)));}
        foreach(var (id,name) in names){using var insert=Command(db,"INSERT OR IGNORE INTO user_names VALUES($u,$n)",("$u",id),("$n",JanjaCord.Shared.NameRules.Key(name)));insert.ExecuteNonQuery();}
    }
    public SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    public static SqliteCommand Command(SqliteConnection db,string sql, params (string,object?)[] args)
    { var cmd=db.CreateCommand(); cmd.CommandText=sql; foreach(var (key,value) in args) cmd.Parameters.AddWithValue(key,value??DBNull.Value); return cmd; }
    public long? User(string token)
    { if(token.Length!=64||!token.All(Uri.IsHexDigit))return null;using var db=Open(); using var cmd=Command(db,"SELECT userId FROM sessions WHERE token=$t AND expires>$e",("$t",TokenHash(token)),("$e",DateTimeOffset.UtcNow.ToString("O"))); return cmd.ExecuteScalar() is long id?id:null; }
    public static string TokenHash(string token)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    public bool Member(long user,long community)
    { using var db=Open(); using var cmd=Command(db,"SELECT 1 FROM members WHERE userId=$u AND communityId=$c",("$u",user),("$c",community)); return cmd.ExecuteScalar()!=null; }
    public bool ChannelMember(long user,long channel)
    { using var db=Open(); using var cmd=Command(db,"SELECT 1 FROM members m JOIN channels c ON c.communityId=m.communityId WHERE m.userId=$u AND c.id=$c",("$u",user),("$c",channel)); return cmd.ExecuteScalar()!=null; }
}
