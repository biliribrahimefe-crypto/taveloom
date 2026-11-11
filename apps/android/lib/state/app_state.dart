import 'dart:async';
import 'dart:math';

import 'package:flutter/foundation.dart';

import '../protocol.dart';
import '../services/host_client.dart';
import '../services/secure_store.dart';

enum Phase { loading, unpaired, paired }

enum ControlPhase { idle, loading, success, error }

class ControlStatus {
  const ControlStatus(this.phase, [this.message]);
  final ControlPhase phase;
  final String? message;
}

class PairProgress {
  const PairProgress({this.stage, this.error});
  final PairStage? stage;
  final PairFailure? error;
}

/// Single source of truth for the UI. All host state arrives as authoritative messages; nothing is invented locally.
class AppState extends ChangeNotifier {
  AppState({SecureStore? store, HostClient? client})
    : _store = store ?? SecureStore(),
      _client = client ?? HostClient();

  final SecureStore _store;
  final HostClient _client;

  Phase phase = Phase.loading;
  StoredHost? host;
  WorkspaceState? workspace;
  Telemetry? telemetry;
  final Map<String, ControlStatus> _status = {};
  final Map<String, String> _requestToControl = {}; // requestId -> controlId
  final Map<String, Timer> _timeouts = {};
  String? toast;
  PairProgress pairProgress = const PairProgress();
  int _toastSerial = 0;
  // Revisions only increase within one host process. After every (re)connect the first state is authoritative,
  // otherwise a restarted host (revision back to 1) would be ignored forever.
  bool _awaitingFirstState = true;
  StreamSubscription<Envelope>? _sub;

  LinkState get link => _client.link.value;
  String? get issue => _client.issue;
  bool get connected => _client.isConnected;
  ControlStatus statusOf(String controlId) =>
      _status[controlId] ?? const ControlStatus(ControlPhase.idle);

  Future<void> init() async {
    _client.link.addListener(_onLink);
    _sub = _client.messages.listen(_onMessage);
    host = await _store.loadHost();
    phase = host == null ? Phase.unpaired : Phase.paired;
    notifyListeners();
    if (host != null) _client.connect(host!);
  }

  // ---- pairing ----

  /// Returns true on success. On failure [pairProgress].error explains why in plain language.
  Future<bool> pair(String qrText, String deviceName) async {
    final qr = PairingQr.tryParse(qrText);
    if (qr == null) {
      pairProgress = PairProgress(
        error: PairFailure(
          'bad_code',
          'That is not a Taveloom pairing code. Open Devices > Pair a phone on your PC and scan that code.',
        ),
      );
      notifyListeners();
      return false;
    }
    pairProgress = const PairProgress(stage: PairStage.connecting);
    notifyListeners();
    try {
      final id = await _store.deviceId();
      final name = deviceName.trim().isEmpty ? 'My Android' : deviceName.trim();
      final stored = await _client.pair(qr, id, name, (s) {
        pairProgress = PairProgress(stage: s);
        notifyListeners();
      });
      await _store.saveHost(stored);
      host = stored;
      phase = Phase.paired;
      pairProgress = const PairProgress();
      notifyListeners();
      _client.connect(stored);
      return true;
    } on PairFailure catch (e) {
      pairProgress = PairProgress(error: e);
      notifyListeners();
      return false;
    }
  }

  void resetPairProgress() {
    pairProgress = const PairProgress();
    notifyListeners();
  }

  /// Forgets this PC on the phone. (Revoke the phone on Windows too if it should lose access there.)
  Future<void> unpair() async {
    await _client.disconnect();
    await _store.clearHost();
    host = null;
    workspace = null;
    telemetry = null;
    _clearPending('Disconnected');
    phase = Phase.unpaired;
    notifyListeners();
  }

  // ---- connection ----

  void _onLink() {
    if (link == LinkState.connected) {
      _awaitingFirstState = true;
    } else {
      _clearPending(
        'Connection lost. The action may or may not have run on the PC.',
      );
    }
    notifyListeners();
  }

  void _clearPending(String message) {
    for (final id in _requestToControl.values.toSet()) {
      if (_status[id]?.phase == ControlPhase.loading) {
        _status[id] = ControlStatus(ControlPhase.error, message);
      }
    }
    for (final t in _timeouts.values) {
      t.cancel();
    }
    _timeouts.clear();
    _requestToControl.clear();
  }

  void _onMessage(Envelope e) {
    switch (e.type) {
      case MessageTypes.workspaceState:
        final s = WorkspaceState.tryParse(e.payload);
        // Outdated or duplicate state must never overwrite newer state.
        if (s != null &&
            (_awaitingFirstState ||
                workspace == null ||
                s.revision > workspace!.revision)) {
          _awaitingFirstState = false;
          workspace = s;
          _client.send(
            Envelope(MessageTypes.stateAck, payload: {'revision': s.revision}),
          );
          notifyListeners();
        }
      case MessageTypes.telemetry:
        telemetry = Telemetry.tryParse(e.payload) ?? telemetry;
        notifyListeners();
      case MessageTypes.actionResult:
        _onResult(ActionResult.tryParse(e.payload));
      case MessageTypes.error:
        final err = ProtocolError.from(e.payload);
        final cid =
            err.requestId == null ? null : _requestToControl[err.requestId];
        if (cid != null) {
          _finish(
            err.requestId!,
            cid,
            ControlStatus(ControlPhase.error, err.message),
          );
        } else {
          _showToast(err.message);
        }
    }
  }

  void _onResult(ActionResult? r) {
    if (r == null) return;
    final cid = _requestToControl[r.requestId];
    if (cid == null) return; // stale or unknown request: ignore
    if (r.state != null) telemetry = r.state;
    _finish(
      r.requestId,
      cid,
      r.ok
          ? ControlStatus(ControlPhase.success, r.message)
          : ControlStatus(
            ControlPhase.error,
            r.error?.message ?? 'The action failed.',
          ),
    );
  }

  void _finish(String requestId, String controlId, ControlStatus s) {
    _timeouts.remove(requestId)?.cancel();
    _requestToControl.remove(requestId);
    _status[controlId] = s;
    notifyListeners();
    if (s.phase == ControlPhase.error && s.message != null) {
      _showToast(s.message!);
    }
    Timer(const Duration(milliseconds: 1400), () {
      if (_status[controlId] == s) {
        _status[controlId] = const ControlStatus(ControlPhase.idle);
        notifyListeners();
      }
    });
  }

  // ---- user intents ----

  static String _newId() {
    final r = Random.secure();
    return List.generate(12, (_) => r.nextInt(36).toRadixString(36)).join();
  }

  /// Press a control. If the PC is not connected nothing is sent and the control shows an error, never a success.
  void invoke(ClientControl c, {double? value}) {
    if (!c.available) {
      _showToast(c.unavailableReason ?? 'This control is not available.');
      return;
    }
    if (statusOf(c.id).phase == ControlPhase.loading &&
        c.widget != WidgetKind.volume) {
      return; // prevent double taps
    }
    final id = _newId();
    final sent = _client.send(
      Envelope(
        MessageTypes.actionInvoke,
        id: id,
        payload: {'controlId': c.id, if (value != null) 'value': value},
      ),
    );
    if (!sent) {
      _status[c.id] = const ControlStatus(
        ControlPhase.error,
        'Not connected to your PC.',
      );
      _showToast('Not connected to your PC.');
      notifyListeners();
      return;
    }
    _requestToControl[id] = c.id;
    _status[c.id] = const ControlStatus(ControlPhase.loading);
    _timeouts[id] = Timer(
      const Duration(seconds: 12),
      () => _finish(
        id,
        c.id,
        const ControlStatus(
          ControlPhase.error,
          'The PC did not answer in time.',
        ),
      ),
    );
    notifyListeners();
  }

  void selectWorkspace(String id) {
    if (!_client.send(
      Envelope(
        MessageTypes.workspaceSelect,
        id: _newId(),
        payload: {'workspaceId': id},
      ),
    )) {
      _showToast('Not connected to your PC.');
    }
  }

  void setAuto(bool enabled) {
    if (!_client.send(
      Envelope(
        MessageTypes.workspaceAuto,
        id: _newId(),
        payload: {'enabled': enabled},
      ),
    )) {
      _showToast('Not connected to your PC.');
    }
  }

  void retryNow() {
    if (host != null) _client.connect(host!);
  }

  void _showToast(String m) {
    toast = m;
    final serial = ++_toastSerial;
    notifyListeners();
    Timer(const Duration(seconds: 4), () {
      if (_toastSerial == serial) {
        toast = null;
        notifyListeners();
      }
    });
  }

  @override
  void dispose() {
    _sub?.cancel();
    _client.link.removeListener(_onLink);
    for (final t in _timeouts.values) {
      t.cancel();
    }
    super.dispose();
  }
}
