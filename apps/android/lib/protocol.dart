// Wire contract shared with the Windows host (see docs/architecture/protocol.md).
// Keep in sync with packages/protocol/Taveloom.Protocol.
import 'dart:convert';
import 'dart:typed_data';

import 'package:crypto/crypto.dart';

const int protocolVersion = 1;

class MessageTypes {
  static const pairRequest = 'pair.request';
  static const pairPending = 'pair.pending';
  static const pairApproved = 'pair.approved';
  static const pairRejected = 'pair.rejected';
  static const authHello = 'auth.hello';
  static const authChallenge = 'auth.challenge';
  static const authResponse = 'auth.response';
  static const authOk = 'auth.ok';
  static const workspaceState = 'workspace.state';
  static const workspaceSelect = 'workspace.select';
  static const workspaceAuto = 'workspace.auto';
  static const stateAck = 'state.ack';
  static const actionInvoke = 'action.invoke';
  static const actionResult = 'action.result';
  static const telemetry = 'telemetry';
  static const ping = 'ping';
  static const pong = 'pong';
  static const error = 'error';
  static const deviceRevoked = 'device.revoked';
}

class ErrorCodes {
  static const authFailed = 'auth_failed';
  static const pairingExpired = 'pairing_expired';
  static const pairingRejected = 'pairing_rejected';
  static const deviceRevoked = 'device_revoked';
  static const rateLimited = 'rate_limited';
  static const unknownControl = 'unknown_control';
  static const actionUnavailable = 'action_unavailable';
  static const timeout = 'timeout';
}

class Envelope {
  Envelope(this.type, {this.id, int? ts, this.payload})
    : ts = ts ?? DateTime.now().millisecondsSinceEpoch;

  final String type;
  final String? id;
  final int ts;
  final Map<String, dynamic>? payload;

  String encode() => jsonEncode({
    'v': protocolVersion,
    'type': type,
    if (id != null) 'id': id,
    'ts': ts,
    if (payload != null) 'payload': payload,
  });

  /// Returns null for anything that is not a valid v1 envelope. Never throws.
  static Envelope? tryParse(Object? raw) {
    if (raw is! String) return null;
    try {
      final j = jsonDecode(raw);
      if (j is! Map<String, dynamic> || j['v'] != protocolVersion) return null;
      final type = j['type'];
      if (type is! String || type.isEmpty) return null;
      final id = j['id'];
      final p = j['payload'];
      return Envelope(
        type,
        id: id is String ? id : null,
        ts: (j['ts'] as num?)?.toInt() ?? 0,
        payload: p is Map<String, dynamic> ? p : null,
      );
    } on FormatException {
      return null;
    }
  }
}

class ProtocolError {
  ProtocolError(this.code, this.message, [this.requestId]);
  final String code;
  final String message;
  final String? requestId;

  factory ProtocolError.from(Map<String, dynamic>? p) => ProtocolError(
    p?['code'] as String? ?? 'unknown',
    p?['message'] as String? ?? 'Unknown error',
    p?['requestId'] as String?,
  );
}

enum WidgetKind { button, toggle, value, media, volume }

class ClientControl {
  ClientControl({
    required this.id,
    required this.label,
    required this.icon,
    required this.widget,
    required this.col,
    required this.row,
    required this.colSpan,
    required this.rowSpan,
    required this.source,
    required this.available,
    required this.unavailableReason,
  });

  final String id, label, icon;
  final WidgetKind widget;
  final int col, row, colSpan, rowSpan;
  final String? source;
  final bool available;
  final String? unavailableReason;

  static ClientControl? tryParse(Object? j) {
    if (j is! Map<String, dynamic>) return null;
    final id = j['id'], label = j['label'];
    if (id is! String || label is! String) return null;
    return ClientControl(
      id: id,
      label: label,
      icon: j['icon'] as String? ?? 'star',
      widget: WidgetKind.values.firstWhere(
        (w) => w.name == j['widget'],
        orElse: () => WidgetKind.button,
      ),
      col: (j['col'] as num?)?.toInt() ?? 0,
      row: (j['row'] as num?)?.toInt() ?? 0,
      colSpan: (j['colSpan'] as num?)?.toInt() ?? 1,
      rowSpan: (j['rowSpan'] as num?)?.toInt() ?? 1,
      source: j['source'] as String?,
      available: j['available'] as bool? ?? true,
      unavailableReason: j['unavailableReason'] as String?,
    );
  }
}

class WorkspaceSummary {
  WorkspaceSummary(this.id, this.name, this.icon);
  final String id, name, icon;
}

class ClientWorkspace {
  ClientWorkspace(
    this.id,
    this.name,
    this.icon,
    this.columns,
    this.rows,
    this.controls,
  );
  final String id, name, icon;
  final int columns, rows;
  final List<ClientControl> controls;
}

class WorkspaceState {
  WorkspaceState({
    required this.revision,
    required this.mode,
    required this.reason,
    required this.active,
    required this.workspaces,
  });

  final int revision;
  final String mode; // 'auto' | 'manual'
  final String reason;
  final ClientWorkspace? active;
  final List<WorkspaceSummary> workspaces;

  static WorkspaceState? tryParse(Map<String, dynamic>? p) {
    if (p == null) return null;
    final rev = (p['revision'] as num?)?.toInt();
    if (rev == null) return null;
    ClientWorkspace? active;
    final a = p['active'];
    if (a is Map<String, dynamic>) {
      active = ClientWorkspace(
        a['id'] as String? ?? '',
        a['name'] as String? ?? '',
        a['icon'] as String? ?? 'star',
        (a['columns'] as num?)?.toInt() ?? 4,
        (a['rows'] as num?)?.toInt() ?? 3,
        (a['controls'] as List? ?? const [])
            .map(ClientControl.tryParse)
            .whereType<ClientControl>()
            .toList(),
      );
    }
    return WorkspaceState(
      revision: rev,
      mode: p['mode'] as String? ?? 'manual',
      reason: p['reason'] as String? ?? '',
      active: active,
      workspaces: [
        for (final w in (p['workspaces'] as List? ?? const []))
          if (w is Map<String, dynamic>)
            WorkspaceSummary(
              w['id'] as String? ?? '',
              w['name'] as String? ?? '',
              w['icon'] as String? ?? 'star',
            ),
      ],
    );
  }
}

/// Live host values. A null field means the host cannot provide it; it is shown as unavailable, never as zero.
class Telemetry {
  Telemetry({this.cpu, this.ram, this.volume, this.muted, this.micMuted});
  final double? cpu, ram, volume;
  final bool? muted, micMuted;

  static Telemetry? tryParse(Map<String, dynamic>? p) =>
      p == null
          ? null
          : Telemetry(
            cpu: (p['cpuPercent'] as num?)?.toDouble(),
            ram: (p['ramPercent'] as num?)?.toDouble(),
            volume: (p['volumePercent'] as num?)?.toDouble(),
            muted: p['muted'] as bool?,
            micMuted: p['micMuted'] as bool?,
          );
}

class ActionResult {
  ActionResult({
    required this.requestId,
    required this.controlId,
    required this.ok,
    required this.message,
    required this.error,
    required this.state,
  });
  final String requestId, controlId;
  final bool ok;
  final String? message;
  final ProtocolError? error;
  final Telemetry? state;

  static ActionResult? tryParse(Map<String, dynamic>? p) {
    if (p == null) return null;
    final rid = p['requestId'], cid = p['controlId'];
    if (rid is! String || cid is! String) return null;
    return ActionResult(
      requestId: rid,
      controlId: cid,
      ok: p['ok'] == true,
      message: p['message'] as String?,
      error:
          p['error'] is Map<String, dynamic>
              ? ProtocolError.from(p['error'] as Map<String, dynamic>)
              : null,
      state: Telemetry.tryParse(p['state'] as Map<String, dynamic>?),
    );
  }
}

/// Contents of the pairing QR code (taveloom://pair?...).
class PairingQr {
  PairingQr({
    required this.hosts,
    required this.port,
    required this.fingerprint,
    required this.token,
    required this.hostName,
  });
  final List<String> hosts;
  final int port;
  final String fingerprint; // SHA-256 of the host certificate, hex
  final String token;
  final String hostName;

  static PairingQr? tryParse(String text) {
    final Uri u;
    try {
      u = Uri.parse(text.trim());
    } on FormatException {
      return null;
    }
    if (u.scheme != 'taveloom' || u.host != 'pair') return null;
    final q = u.queryParameters;
    if (q['v'] != '$protocolVersion') return null;
    final hosts =
        (q['h'] ?? '')
            .split(',')
            .map((h) => h.trim())
            .where((h) => h.isNotEmpty)
            .toList();
    final port = int.tryParse(q['p'] ?? '');
    final f = (q['f'] ?? '').toUpperCase();
    final t = q['t'] ?? '';
    if (hosts.isEmpty || port == null || port < 1 || port > 65535) return null;
    if (!RegExp(r'^[0-9A-F]{64}$').hasMatch(f) || t.length < 16) return null;
    return PairingQr(
      hosts: hosts,
      port: port,
      fingerprint: f,
      token: t,
      hostName: q['n'] ?? 'Windows PC',
    );
  }
}

String base64UrlNoPad(List<int> bytes) =>
    base64Url.encode(bytes).replaceAll('=', '');
Uint8List decodeBase64UrlNoPad(String s) =>
    base64Url.decode(base64Url.normalize(s));

/// HMAC-SHA256(credential, "taveloom-auth-v1|nonce|deviceId"), base64url without padding.
String authProof(List<int> credential, String nonce, String deviceId) =>
    base64UrlNoPad(
      Hmac(
        sha256,
        credential,
      ).convert(utf8.encode('taveloom-auth-v1|$nonce|$deviceId')).bytes,
    );
