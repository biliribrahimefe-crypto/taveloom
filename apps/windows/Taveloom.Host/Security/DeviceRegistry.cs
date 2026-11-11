using System.Security.Cryptography;
using Taveloom.Host.Storage;

namespace Taveloom.Host.Security;

public sealed record TrustedDevice(string Id, string Name, DateTimeOffset PairedAt, DateTimeOffset? LastSeen, bool Revoked);

/// <summary>Trusted devices and their credentials. Credentials are protected with Windows DPAPI (current user) at rest.</summary>
public sealed class DeviceRegistry(Database db, AuditLog audit)
{
    private static readonly byte[] Entropy = "Taveloom.DeviceCredential.v1"u8.ToArray();
    public event Action<string>? Revoked;
    public event Action? Changed;

    public static bool IsValidDeviceId(string? id) => id is { Length: >= 8 and <= 64 } && id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    public byte[] AddOrReplace(string id, string name)
    {
        var credential = RandomNumberGenerator.GetBytes(32);
        var protectedBlob = ProtectedData.Protect(credential, Entropy, DataProtectionScope.CurrentUser);
        db.Run("""
            INSERT INTO devices(id,name,credential,paired_at,last_seen,revoked) VALUES($i,$n,$c,$p,NULL,0)
            ON CONFLICT(id) DO UPDATE SET name=$n, credential=$c, paired_at=$p, revoked=0
            """, ("$i", id), ("$n", Clean(name)), ("$c", protectedBlob), ("$p", Now()));
        audit.Write("device.paired", id);
        Changed?.Invoke();
        return credential;
    }

    /// <summary>Returns the credential for an active device, or null if the device is unknown or revoked.</summary>
    public byte[]? GetCredential(string id)
    {
        var rows = db.Query("SELECT credential FROM devices WHERE id=$i AND revoked=0", r => (byte[])r[0], ("$i", id));
        if (rows.Count == 0) return null;
        try { return ProtectedData.Unprotect(rows[0], Entropy, DataProtectionScope.CurrentUser); }
        catch (CryptographicException) { return null; }
    }

    public bool IsRevoked(string id) =>
        db.Query("SELECT revoked FROM devices WHERE id=$i", r => r.GetInt32(0) == 1, ("$i", id)).FirstOrDefault();

    public List<TrustedDevice> List() => db.Query(
        "SELECT id,name,paired_at,last_seen,revoked FROM devices ORDER BY paired_at DESC",
        r => new TrustedDevice(r.GetString(0), r.GetString(1), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(2)),
            r.IsDBNull(3) ? null : DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(3)), r.GetInt32(4) == 1));

    public void Touch(string id) => db.Run("UPDATE devices SET last_seen=$t WHERE id=$i", ("$t", Now()), ("$i", id));

    public void Rename(string id, string name)
    {
        db.Run("UPDATE devices SET name=$n WHERE id=$i", ("$n", Clean(name)), ("$i", id));
        Changed?.Invoke();
    }

    /// <summary>Revokes the credential. The device must pair again from scratch; live sessions are closed by listeners.</summary>
    public void Revoke(string id)
    {
        db.Run("UPDATE devices SET revoked=1 WHERE id=$i", ("$i", id));
        audit.Write("device.revoked", id);
        Revoked?.Invoke(id);
        Changed?.Invoke();
    }

    public void Forget(string id)
    {
        db.Run("DELETE FROM devices WHERE id=$i", ("$i", id));
        Changed?.Invoke();
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static string Clean(string name) { name = name.Trim(); return name.Length == 0 ? "Android device" : name[..Math.Min(name.Length, 40)]; }
}
