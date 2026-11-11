namespace Taveloom.Protocol;

public static class MessageTypes
{
    public const string PairRequest = "pair.request", PairPending = "pair.pending", PairApproved = "pair.approved", PairRejected = "pair.rejected";
    public const string AuthHello = "auth.hello", AuthChallenge = "auth.challenge", AuthResponse = "auth.response", AuthOk = "auth.ok";
    public const string WorkspaceState = "workspace.state", WorkspaceSelect = "workspace.select", WorkspaceAuto = "workspace.auto", StateAck = "state.ack";
    public const string ActionInvoke = "action.invoke", ActionResult = "action.result", Telemetry = "telemetry";
    public const string Ping = "ping", Pong = "pong", Error = "error", DeviceRevoked = "device.revoked";
}

public static class ErrorCodes
{
    public const string InvalidMessage = "invalid_message", UnsupportedVersion = "unsupported_version", Unauthenticated = "unauthenticated";
    public const string AuthFailed = "auth_failed", PairingExpired = "pairing_expired", PairingRejected = "pairing_rejected";
    public const string DeviceRevoked = "device_revoked", RateLimited = "rate_limited", UnknownControl = "unknown_control";
    public const string ActionUnavailable = "action_unavailable", ActionFailed = "action_failed", Timeout = "timeout";
    public const string UnknownWorkspace = "unknown_workspace", Internal = "internal";
}

public enum WidgetType { Button, Toggle, Value, Media, Volume }

public sealed record ErrorPayload(string Code, string Message, string? RequestId = null);

public sealed record PairRequestPayload(string Token, string DeviceId, string DeviceName);
public sealed record PairApprovedPayload(string DeviceId, string Credential, string HostName);
public sealed record AuthHelloPayload(string DeviceId);
public sealed record AuthChallengePayload(string Nonce);
public sealed record AuthResponsePayload(string Proof);
public sealed record AuthOkPayload(string HostName, int ProtocolVersion);

public sealed record ActionInvokePayload(string ControlId, double? Value = null);
public sealed record ActionResultPayload(string RequestId, string ControlId, bool Ok, long Timestamp, ErrorPayload? Error, string? Message, TelemetryPayload? State);

public sealed record WorkspaceSelectPayload(string WorkspaceId);
public sealed record WorkspaceAutoPayload(bool Enabled);
public sealed record StateAckPayload(long Revision);

/// <summary>Live values. Null means "not available on this host", never a fabricated number.</summary>
public sealed record TelemetryPayload(double? CpuPercent, double? RamPercent, double? VolumePercent, bool? Muted, bool? MicMuted = null);

public sealed record WorkspaceSummary(string Id, string Name, string Icon);

public sealed record ClientControl(string Id, string Label, string Icon, WidgetType Widget,
    int Col, int Row, int ColSpan, int RowSpan, string? Source, bool Available, string? UnavailableReason);

public sealed record ClientWorkspace(string Id, string Name, string Icon, int Columns, int Rows, IReadOnlyList<ClientControl> Controls);

/// <summary>Authoritative workspace state. Clients must ignore any state whose Revision is not greater than the one they hold.</summary>
public sealed record WorkspaceStatePayload(long Revision, string Mode, string Reason, ClientWorkspace? Active, IReadOnlyList<WorkspaceSummary> Workspaces);
