import 'dart:async';
import 'dart:io';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:flutter/foundation.dart';

import '../protocol.dart';
import 'secure_store.dart';

enum LinkState {
  disconnected,
  connecting,
  connected,
  reconnecting,
  revoked,
  authFailed,
  untrusted,
}

enum PairStage { connecting, awaitingApproval }

class PairFailure implements Exception {
  PairFailure(this.code, this.message);
  final String code;
  final String message;
  @override
  String toString() => message;
}

enum _End {
  lostAfterConnect,
  failedBeforeConnect,
  revoked,
  authFailed,
  rateLimited,
}

/// Secure WebSocket client. TLS trust comes only from the certificate fingerprint delivered by the pairing QR code:
/// no system CAs are trusted for this connection and verification is never disabled.
class HostClient {
  final _messages = StreamController<Envelope>.broadcast();
  final ValueNotifier<LinkState> link = ValueNotifier(LinkState.disconnected);

  WebSocket? _ws;
  StoredHost? _host;
  bool _stopped = true;
  int _generation = 0;

  /// Human-readable detail for the current non-connected state (shown in the UI).
  String? issue;

  Stream<Envelope> get messages => _messages.stream;
  bool get isConnected => link.value == LinkState.connected && _ws != null;

  static String _fingerprint(Uint8List der) =>
      sha256.convert(der).toString().toUpperCase();

  Future<WebSocket> _open(
    List<String> hosts,
    int port,
    String fingerprint,
  ) async {
    Object? last;
    for (final h in hosts) {
      final client =
          HttpClient(context: SecurityContext(withTrustedRoots: false))
            ..connectionTimeout = const Duration(seconds: 4)
            ..badCertificateCallback =
                (cert, host, p) =>
                    _fingerprint(cert.der) == fingerprint.toUpperCase();
      try {
        final ws = await WebSocket.connect(
          'wss://$h:$port/ws',
          customClient: client,
        ).timeout(const Duration(seconds: 6));
        ws.pingInterval = const Duration(seconds: 10);
        return ws;
      } on HandshakeException catch (e) {
        client.close(force: true);
        last =
            e; // fingerprint mismatch: keep trying other addresses, but remember why
      } catch (e) {
        client.close(force: true);
        last = e;
      }
    }
    throw last ?? const SocketException('No host address reachable');
  }

  /// One-time pairing. Resolves with the stored credential after the user approves the device on Windows.
  Future<StoredHost> pair(
    PairingQr qr,
    String deviceId,
    String deviceName,
    void Function(PairStage) onStage,
  ) async {
    onStage(PairStage.connecting);
    final WebSocket ws;
    try {
      ws = await _open(qr.hosts, qr.port, qr.fingerprint);
    } on HandshakeException {
      throw PairFailure(
        'untrusted',
        'This is not the PC in the QR code (its security certificate did not match). Scan a new code.',
      );
    } catch (_) {
      throw PairFailure(
        'unreachable',
        'Could not reach ${qr.hostName}. Check that your phone and PC are on the same Wi-Fi network and that Taveloom is running.',
      );
    }

    final done = Completer<StoredHost>();
    final sub = ws.listen(
      (data) {
        final e = Envelope.tryParse(data);
        if (e == null || done.isCompleted) return;
        switch (e.type) {
          case MessageTypes.pairPending:
            onStage(PairStage.awaitingApproval);
          case MessageTypes.pairApproved:
            final cred = e.payload?['credential'];
            if (cred is String) {
              done.complete(
                StoredHost(
                  deviceId: deviceId,
                  credential: cred,
                  hosts: qr.hosts,
                  port: qr.port,
                  fingerprint: qr.fingerprint,
                  hostName: (e.payload?['hostName'] as String?) ?? qr.hostName,
                ),
              );
            }
          case MessageTypes.error:
            final err = ProtocolError.from(e.payload);
            done.completeError(PairFailure(err.code, err.message));
        }
      },
      onDone:
          () =>
              done.isCompleted
                  ? null
                  : done.completeError(
                    PairFailure(
                      'lost',
                      'The connection closed before pairing finished.',
                    ),
                  ),
      onError:
          (_) =>
              done.isCompleted
                  ? null
                  : done.completeError(
                    PairFailure('lost', 'The connection was interrupted.'),
                  ),
    );

    ws.add(
      Envelope(
        MessageTypes.pairRequest,
        id: 'pair',
        payload: {
          'token': qr.token,
          'deviceId': deviceId,
          'deviceName': deviceName,
        },
      ).encode(),
    );
    try {
      return await done.future.timeout(
        const Duration(seconds: 75),
        onTimeout:
            () =>
                throw PairFailure(
                  'timeout',
                  'Nobody approved the request on the PC in time. Try again.',
                ),
      );
    } finally {
      await sub.cancel();
      await ws.close();
    }
  }

  /// Starts (or restarts) the maintain-connection loop for a paired host.
  void connect(StoredHost host) {
    _generation++;
    _stopped = false;
    _host = host;
    issue = null;
    unawaited(_loop(_generation));
  }

  Future<void> disconnect() async {
    _stopped = true;
    _generation++;
    final ws = _ws;
    _ws = null;
    await ws?.close();
    _setLink(LinkState.disconnected);
  }

  void _setLink(LinkState s) {
    if (link.value != s) link.value = s;
  }

  Future<void> _loop(int gen) async {
    var attempt = 0;
    while (!_stopped && gen == _generation) {
      final host = _host!;
      _setLink(attempt == 0 ? LinkState.connecting : LinkState.reconnecting);
      var wait = Duration(seconds: min(15, 1 << min(attempt, 4)));
      try {
        final ws = await _open(host.hosts, host.port, host.fingerprint);
        if (_stopped || gen != _generation) {
          await ws.close();
          return;
        }
        _ws = ws;
        final end = await _session(ws, host, gen);
        _ws = null;
        switch (end) {
          case _End.revoked:
            issue =
                'This phone was removed on the PC. Pair it again to keep using it.';
            _setLink(LinkState.revoked);
            return;
          case _End.authFailed:
            issue = 'The PC no longer recognises this phone. Pair it again.';
            _setLink(LinkState.authFailed);
            return;
          case _End.rateLimited:
            wait = const Duration(seconds: 60);
          case _End.lostAfterConnect:
            attempt = 0;
            wait = const Duration(seconds: 1);
          case _End.failedBeforeConnect:
            break;
        }
      } on HandshakeException {
        issue =
            'The PC\'s security identity changed, so Taveloom refused to connect. Pair again if you trust this network.';
        _setLink(LinkState.untrusted);
        return;
      } catch (_) {
        issue =
            'Cannot reach ${host.hostName}. Is it on and running Taveloom, on the same network?';
      }
      if (_stopped || gen != _generation) return;
      _setLink(LinkState.reconnecting);
      attempt++;
      await Future<void>.delayed(wait);
    }
  }

  Future<_End> _session(WebSocket ws, StoredHost host, int gen) {
    final result = Completer<_End>();
    var authed = false;
    var lastRx = DateTime.now();
    Timer? heartbeat;

    void finish(_End e) {
      heartbeat?.cancel();
      if (!result.isCompleted) result.complete(e);
      ws.close().ignore();
    }

    ws.listen(
      (data) {
        lastRx = DateTime.now();
        final e = Envelope.tryParse(data);
        if (e == null) return;
        switch (e.type) {
          case MessageTypes.authChallenge:
            final nonce = e.payload?['nonce'];
            if (nonce is String) {
              final proof = authProof(
                decodeBase64UrlNoPad(host.credential),
                nonce,
                host.deviceId,
              );
              ws.add(
                Envelope(
                  MessageTypes.authResponse,
                  payload: {'proof': proof},
                ).encode(),
              );
            }
          case MessageTypes.authOk:
            authed = true;
            issue = null;
            _setLink(LinkState.connected);
            heartbeat = Timer.periodic(const Duration(seconds: 8), (_) {
              if (DateTime.now().difference(lastRx) >
                  const Duration(seconds: 25)) {
                finish(_End.lostAfterConnect);
              } else {
                ws.add(Envelope(MessageTypes.ping).encode());
              }
            });
          case MessageTypes.deviceRevoked:
            finish(_End.revoked);
          case MessageTypes.error when !authed:
            switch (ProtocolError.from(e.payload).code) {
              case ErrorCodes.deviceRevoked:
                finish(_End.revoked);
              case ErrorCodes.authFailed:
                finish(_End.authFailed);
              case ErrorCodes.rateLimited:
                finish(_End.rateLimited);
            }
          case MessageTypes.pong:
            break;
          default:
            if (authed) _messages.add(e);
        }
      },
      onDone:
          () =>
              finish(authed ? _End.lostAfterConnect : _End.failedBeforeConnect),
      onError:
          (_) =>
              finish(authed ? _End.lostAfterConnect : _End.failedBeforeConnect),
      cancelOnError: true,
    );

    ws.add(
      Envelope(
        MessageTypes.authHello,
        payload: {'deviceId': host.deviceId},
      ).encode(),
    );
    Timer(const Duration(seconds: 15), () {
      if (!authed) finish(_End.failedBeforeConnect);
    });
    return result.future;
  }

  /// Sends only while authenticated. Returns false if the message could not be sent (so callers never show false success).
  bool send(Envelope e) {
    final ws = _ws;
    if (!isConnected || ws == null) return false;
    try {
      ws.add(e.encode());
      return true;
    } catch (_) {
      return false;
    }
  }
}
