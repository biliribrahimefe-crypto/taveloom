using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Taveloom.Protocol;

namespace Taveloom.Host.Security;

public sealed record PairingOffer(string Token, DateTimeOffset ExpiresAt);
public enum TokenResult { Ok, Unknown, Expired }
public sealed record PendingPairing(string RequestId, string DeviceId, string DeviceName, string RemoteAddress, DateTimeOffset RequestedAt);

/// <summary>
/// One-time, short-lived pairing tokens plus the explicit human approval step on Windows.
/// A device is only trusted after (1) presenting a valid token from the QR code AND (2) the Windows user approving it.
/// </summary>
public sealed class PairingService(Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = new();

    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan ApprovalTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Raised when a device with a valid token asks to pair. If nobody handles it the request is rejected.</summary>
    public event Action<PendingPairing>? ApprovalRequested;
    public event Action<string>? ApprovalResolved;

    public PairingOffer CreateOffer()
    {
        Purge();
        var token = AuthProof.RandomToken(24);
        var exp = _now() + TokenLifetime;
        _tokens[token] = exp;
        return new PairingOffer(token, exp);
    }

    /// <summary>Consumes a token (single use). Comparison walks every stored token so timing does not reveal prefix matches.</summary>
    public TokenResult Consume(string? token)
    {
        if (string.IsNullOrEmpty(token)) return TokenResult.Unknown;
        string? hit = null;
        foreach (var k in _tokens.Keys)
            if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(k), Encoding.UTF8.GetBytes(token))) hit = k;
        if (hit is null || !_tokens.TryRemove(hit, out var exp)) return TokenResult.Unknown;
        return exp < _now() ? TokenResult.Expired : TokenResult.Ok;
    }

    public async Task<bool> RequestApprovalAsync(string deviceId, string deviceName, string remote, CancellationToken ct)
    {
        var handler = ApprovalRequested;
        if (handler is null) return false;
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            handler(new PendingPairing(id, deviceId, deviceName, remote, _now()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ApprovalTimeout);
            await using (cts.Token.Register(() => tcs.TrySetResult(false)))
                return await tcs.Task;
        }
        finally { _pending.TryRemove(id, out _); ApprovalResolved?.Invoke(id); }
    }

    public void Resolve(string requestId, bool approved)
    {
        if (_pending.TryGetValue(requestId, out var tcs)) tcs.TrySetResult(approved);
    }

    private void Purge()
    {
        foreach (var (k, exp) in _tokens) if (exp < _now()) _tokens.TryRemove(k, out _);
    }
}

/// <summary>Blocks an address after repeated authentication failures.</summary>
public sealed class AuthThrottle(Func<DateTimeOffset>? clock = null, int maxFailures = 5)
{
    private readonly Func<DateTimeOffset> _now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _failures = new();
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);

    public bool IsBlocked(string key)
    {
        if (!_failures.TryGetValue(key, out var list)) return false;
        lock (list) { list.RemoveAll(t => t < _now() - Window); return list.Count >= maxFailures; }
    }

    public void RecordFailure(string key)
    {
        var list = _failures.GetOrAdd(key, _ => []);
        lock (list) list.Add(_now());
    }

    public void Reset(string key) => _failures.TryRemove(key, out _);
}
