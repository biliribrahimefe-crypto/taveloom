using Microsoft.Data.Sqlite;

namespace Taveloom.Host.Storage;

/// <summary>SQLite persistence. One short-lived connection per operation; WAL journal; schema created on open.</summary>
public sealed class Database
{
    private readonly string _cs;

    public Database(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _cs = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        using var c = Open();
        Exec(c, "PRAGMA journal_mode=WAL;");
        Exec(c, """
            CREATE TABLE IF NOT EXISTS devices(id TEXT PRIMARY KEY, name TEXT NOT NULL, credential BLOB NOT NULL,
                paired_at INTEGER NOT NULL, last_seen INTEGER, revoked INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS workspaces(id TEXT PRIMARY KEY, sort INTEGER NOT NULL, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL, kind TEXT NOT NULL, device_id TEXT, detail TEXT);
            """);
    }

    public SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        return c;
    }

    public static void Exec(SqliteConnection c, string sql, params (string, object?)[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void Run(string sql, params (string, object?)[] args)
    {
        using var c = Open();
        Exec(c, sql, args);
    }

    public List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string, object?)[] args)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }
}

public sealed class SettingsStore(Database db)
{
    public string? Get(string key) => db.Query("SELECT value FROM settings WHERE key=$k", r => r.GetString(0), ("$k", key)).FirstOrDefault();
    public void Set(string key, string value) =>
        db.Run("INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v", ("$k", key), ("$v", value));
    public bool GetBool(string key, bool fallback) => Get(key) is { } v ? v == "1" : fallback;
    public void SetBool(string key, bool value) => Set(key, value ? "1" : "0");
}

/// <summary>Audit trail. Records what happened and to which device, never credentials, tokens or user data.</summary>
public sealed class AuditLog(Database db)
{
    public void Write(string kind, string? deviceId = null, string? detail = null) =>
        db.Run("INSERT INTO audit(ts,kind,device_id,detail) VALUES($t,$k,$d,$x)",
            ("$t", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), ("$k", kind), ("$d", deviceId), ("$x", detail));

    public List<(long Ts, string Kind, string? DeviceId, string? Detail)> Recent(int count = 100) =>
        db.Query("SELECT ts,kind,device_id,detail FROM audit ORDER BY id DESC LIMIT $n",
            r => (r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3)), ("$n", count));
}
