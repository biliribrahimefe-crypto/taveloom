// Runs the real Android client code (HostClient/protocol) against a real Taveloom Windows host on loopback.
// It is skipped unless TAVELOOM_E2E_URI is set. See docs/development/building.md ("End-to-end against the real host").
// This is NOT a device test: no phone, camera or Android runtime is involved.
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:taveloom/protocol.dart';
import 'package:taveloom/services/host_client.dart';

Future<void> waitFor(
  bool Function() ok,
  String what, {
  int seconds = 10,
}) async {
  final end = DateTime.now().add(Duration(seconds: seconds));
  while (!ok()) {
    if (DateTime.now().isAfter(end)) fail('Timed out waiting for: $what');
    await Future<void>.delayed(const Duration(milliseconds: 50));
  }
}

void main() {
  final uri = Platform.environment['TAVELOOM_E2E_URI'];
  final flagDir = Platform.environment['TAVELOOM_E2E_FLAGDIR'];
  final hostLog = Platform.environment['TAVELOOM_E2E_HOSTLOG'];

  // Number of authenticated sessions the host currently sees (last "SESSIONS n" line of its log).
  int hostSessions() =>
      RegExp(r'SESSIONS (\d+)')
          .allMatches(File(hostLog!).readAsStringSync())
          .map((m) => int.parse(m.group(1)!))
          .last;

  test(
    'pair, authenticate, sync, act, reconnect and get revoked against the real host',
    skip:
        uri == null || flagDir == null || hostLog == null
            ? 'TAVELOOM_E2E_URI / TAVELOOM_E2E_FLAGDIR not set'
            : false,
    timeout: const Timeout(Duration(minutes: 2)),
    () async {
      final qr = PairingQr.tryParse(uri!)!;

      // A host whose certificate does not match the pinned fingerprint is refused, and the token is not spent.
      final wrongPin = PairingQr(
        hosts: qr.hosts,
        port: qr.port,
        fingerprint: '0' * 64,
        token: qr.token,
        hostName: qr.hostName,
      );
      await expectLater(
        HostClient().pair(wrongPin, 'and-e2e0123456789ab', 'Imposter', (_) {}),
        throwsA(isA<PairFailure>().having((e) => e.code, 'code', 'untrusted')),
      );

      // Pairing (the harness auto-approves).
      final stages = <PairStage>[];
      final client = HostClient();
      final stored = await client.pair(
        qr,
        'and-e2e0123456789ab',
        'E2E Phone',
        stages.add,
      );
      expect(stages, contains(PairStage.awaitingApproval));
      expect(stored.credential, isNotEmpty);

      // A second use of the same QR token must fail.
      await expectLater(
        HostClient().pair(qr, 'and-other0123456789', 'Replay', (_) {}),
        throwsA(
          isA<PairFailure>().having((e) => e.code, 'code', 'auth_failed'),
        ),
      );

      // Authenticated session + state sync.
      final msgs = <Envelope>[];
      client.messages.listen(msgs.add);
      client.connect(stored);
      await waitFor(() => client.isConnected, 'link connected');
      await waitFor(
        () => msgs.any((m) => m.type == MessageTypes.workspaceState),
        'first workspace.state',
      );
      var state =
          WorkspaceState.tryParse(
            msgs
                .firstWhere((m) => m.type == MessageTypes.workspaceState)
                .payload,
          )!;
      expect(
        state.workspaces.map((w) => w.name),
        containsAll(['Development', 'Productivity', 'Media']),
      );

      WorkspaceState? latest() => msgs
          .where((m) => m.type == MessageTypes.workspaceState)
          .map((m) => WorkspaceState.tryParse(m.payload))
          .whereType<WorkspaceState>()
          .fold<WorkspaceState?>(
            null,
            (a, b) => a == null || b.revision >= a.revision ? b : a,
          );

      // Select the Media workspace.
      final media = state.workspaces.firstWhere((w) => w.name == 'Media');
      expect(
        client.send(
          Envelope(
            MessageTypes.workspaceSelect,
            id: 'sel1',
            payload: {'workspaceId': media.id},
          ),
        ),
        isTrue,
      );
      await waitFor(
        () => latest()?.active?.name == 'Media',
        'Media workspace active',
      );
      state = latest()!;
      expect(state.mode, 'manual');

      // Real action over the wire: media key reaches the host's input sender and the result is reported.
      final play = state.active!.controls.firstWhere(
        (c) => c.label == 'Play / Pause',
      );
      client.send(
        Envelope(
          MessageTypes.actionInvoke,
          id: 'act1',
          payload: {'controlId': play.id},
        ),
      );
      await waitFor(
        () => msgs.any(
          (m) => m.type == MessageTypes.actionResult && m.id == 'act1',
        ),
        'action.result act1',
      );
      final res =
          ActionResult.tryParse(
            msgs
                .firstWhere(
                  (m) => m.type == MessageTypes.actionResult && m.id == 'act1',
                )
                .payload,
          )!;
      expect(res.ok, isTrue, reason: res.error?.message);

      // Volume slider value is applied and reported back.
      final vol = state.active!.controls.firstWhere(
        (c) => c.widget == WidgetKind.volume,
      );
      client.send(
        Envelope(
          MessageTypes.actionInvoke,
          id: 'vol1',
          payload: {'controlId': vol.id, 'value': 55},
        ),
      );
      await waitFor(
        () => msgs.any(
          (m) => m.type == MessageTypes.actionResult && m.id == 'vol1',
        ),
        'action.result vol1',
      );
      expect(
        ActionResult.tryParse(
          msgs
              .firstWhere(
                (m) => m.id == 'vol1' && m.type == MessageTypes.actionResult,
              )
              .payload,
        )!.state?.volume,
        55,
      );

      // Telemetry is pushed.
      await waitFor(
        () => msgs.any((m) => m.type == MessageTypes.telemetry),
        'telemetry',
        seconds: 8,
      );

      // Regression: restarting connect() repeatedly must leave exactly one healthy session.
      client.connect(stored);
      client.connect(stored);
      await waitFor(() => client.isConnected, 'reconnected after restart');
      await Future<void>.delayed(const Duration(seconds: 2));
      expect(client.isConnected, isTrue);
      expect(
        hostSessions(),
        1,
        reason:
            'restarting connect() must not leave an extra live session on the host',
      );
      client.send(
        Envelope(
          MessageTypes.actionInvoke,
          id: 'act2',
          payload: {'controlId': play.id},
        ),
      );
      await waitFor(
        () => msgs.any(
          (m) => m.type == MessageTypes.actionResult && m.id == 'act2',
        ),
        'action.result act2',
      );

      // Revocation on the host disconnects the phone and blocks reconnecting.
      File('$flagDir/revoke.flag').writeAsStringSync('1');
      await waitFor(
        () => client.link.value == LinkState.revoked,
        'link revoked',
      );
      expect(client.issue, isNotNull);
      expect(client.send(Envelope(MessageTypes.ping)), isFalse);
      await client.disconnect();
    },
  );
}
