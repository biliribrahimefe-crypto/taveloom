using System.Text.Json;
using System.Text.Json.Serialization;

namespace Taveloom.Protocol;

public static class ProtocolInfo
{
    public const int Version = 1;
    public const int MaxMessageBytes = 64 * 1024;
}

public static class ProtocolJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

/// <summary>Versioned wire envelope. Every message in both directions uses this shape.</summary>
public sealed record Envelope(int V, string Type, string? Id, long Ts, JsonElement? Payload)
{
    public static Envelope Create<T>(string type, T payload, string? id = null) =>
        new(ProtocolInfo.Version, type, id, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            JsonSerializer.SerializeToElement(payload, ProtocolJson.Options));

    public static Envelope Empty(string type, string? id = null) =>
        new(ProtocolInfo.Version, type, id, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), null);

    public byte[] ToUtf8() => JsonSerializer.SerializeToUtf8Bytes(this, ProtocolJson.Options);

    /// <summary>Parses and structurally validates an incoming message. Never throws.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8, out Envelope envelope, out string error)
    {
        envelope = null!;
        if (utf8.Length == 0 || utf8.Length > ProtocolInfo.MaxMessageBytes) { error = "Message size out of range."; return false; }
        try
        {
            var e = JsonSerializer.Deserialize<Envelope>(utf8, ProtocolJson.Options);
            if (e is null) { error = "Empty message."; return false; }
            if (e.V != ProtocolInfo.Version) { error = $"Unsupported protocol version {e.V}."; return false; }
            if (string.IsNullOrWhiteSpace(e.Type) || e.Type.Length > 64) { error = "Invalid message type."; return false; }
            if (e.Id is { Length: > 64 }) { error = "Invalid message id."; return false; }
            envelope = e; error = ""; return true;
        }
        catch (JsonException) { error = "Malformed JSON."; return false; }
    }

    public bool TryReadPayload<T>(out T value) where T : class
    {
        value = null!;
        if (Payload is not { ValueKind: JsonValueKind.Object } p) return false;
        try { var v = p.Deserialize<T>(ProtocolJson.Options); if (v is null) return false; value = v; return true; }
        catch (JsonException) { return false; }
    }
}
