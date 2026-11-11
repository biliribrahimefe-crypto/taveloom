using System.Security.Cryptography;
using System.Text;

namespace Taveloom.Protocol;

/// <summary>Contents of the pairing QR code: where the host is, which certificate to pin, and a short-lived one-time token.</summary>
public sealed record PairingQr(IReadOnlyList<string> Hosts, int Port, string Fingerprint, string Token, string HostName)
{
    public string ToUri() =>
        $"taveloom://pair?v={ProtocolInfo.Version}&h={Uri.EscapeDataString(string.Join(',', Hosts))}&p={Port}" +
        $"&f={Fingerprint}&t={Uri.EscapeDataString(Token)}&n={Uri.EscapeDataString(HostName)}";

    public static bool TryParse(string uri, out PairingQr qr)
    {
        qr = null!;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u) || u.Scheme != "taveloom" || u.Host != "pair") return false;
        var q = System.Web.HttpUtility.ParseQueryString(u.Query);
        if (q["v"] != ProtocolInfo.Version.ToString()) return false;
        var hosts = (q["h"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length == 0 || !int.TryParse(q["p"], out var port) || port is < 1 or > 65535) return false;
        var f = q["f"] ?? ""; var t = q["t"] ?? "";
        if (f.Length != 64 || !f.All(Uri.IsHexDigit) || t.Length < 16) return false;
        qr = new PairingQr(hosts, port, f.ToUpperInvariant(), t, q["n"] ?? "Windows PC");
        return true;
    }
}

public static class AuthProof
{
    /// <summary>HMAC-SHA256 over the server nonce and device id, keyed by the device credential. The credential itself never crosses the wire after pairing.</summary>
    public static string Compute(byte[] credential, string nonce, string deviceId)
    {
        using var h = new HMACSHA256(credential);
        return System.Buffers.Text.Base64Url.EncodeToString(h.ComputeHash(Encoding.UTF8.GetBytes($"taveloom-auth-v1|{nonce}|{deviceId}")));
    }

    public static bool Verify(byte[] credential, string nonce, string deviceId, string proof) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Compute(credential, nonce, deviceId)), Encoding.ASCII.GetBytes(proof));

    public static string RandomToken(int bytes = 32) => System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(bytes));
}
