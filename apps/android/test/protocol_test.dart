import 'package:flutter_test/flutter_test.dart';
import 'package:taveloom/protocol.dart';

void main() {
  test('auth proof matches the Windows host (cross-language vector)', () {
    final key = List<int>.generate(32, (i) => i);
    expect(authProof(key, 'nonce-abc', 'dev-12345678'), 'EKj-sPXs0muqUyu7ZrbbJgMoc1WosmlfHWYJT_3cy6A');
  });

  test('envelope round trip and rejection of bad input', () {
    final e = Envelope(MessageTypes.actionInvoke, id: 'r1', payload: {'controlId': 'c1', 'value': 42});
    final back = Envelope.tryParse(e.encode())!;
    expect(back.type, 'action.invoke');
    expect(back.id, 'r1');
    expect(back.payload?['value'], 42);
    for (final bad in ['not json', '{}', '[]', 'null', '{"v":2,"type":"x","ts":1}', '{"v":1,"type":"","ts":1}']) {
      expect(Envelope.tryParse(bad), isNull, reason: bad);
    }
    expect(Envelope.tryParse(null), isNull);
    expect(Envelope.tryParse(<int>[1, 2, 3]), isNull);
  });

  test('pairing QR parsing', () {
    final fp = 'A' * 64;
    final qr = PairingQr.tryParse('taveloom://pair?v=1&h=192.168.1.5%2C10.0.0.2&p=47821&f=$fp&t=tok-0123456789abcdef&n=My%20PC')!;
    expect(qr.hosts, ['192.168.1.5', '10.0.0.2']);
    expect(qr.port, 47821);
    expect(qr.hostName, 'My PC');
    for (final bad in [
      'https://evil.example/pair',
      'taveloom://pair?v=9&h=1.1.1.1&p=1&f=$fp&t=tok-0123456789abcdef',
      'taveloom://pair?v=1&h=1.1.1.1&p=99999&f=$fp&t=tok-0123456789abcdef',
      'taveloom://pair?v=1&h=1.1.1.1&p=1&f=short&t=tok-0123456789abcdef',
      'garbage',
    ]) {
      expect(PairingQr.tryParse(bad), isNull, reason: bad);
    }
  });

  test('workspace state parsing keeps unavailable reasons and tolerates junk', () {
    final s = WorkspaceState.tryParse({
      'revision': 5,
      'mode': 'auto',
      'reason': 'Rule: Code.exe',
      'active': {
        'id': 'w',
        'name': 'Dev',
        'icon': 'code',
        'columns': 4,
        'rows': 3,
        'controls': [
          {'id': 'a', 'label': 'Project', 'icon': 'folder', 'widget': 'button', 'col': 2, 'row': 0, 'colSpan': 1, 'rowSpan': 1, 'available': false, 'unavailableReason': 'Not configured.'},
          'junk',
          {'id': 'v', 'label': 'Vol', 'widget': 'volume', 'col': 0, 'row': 1, 'colSpan': 2, 'rowSpan': 1, 'available': true},
        ],
      },
      'workspaces': [
        {'id': 'w', 'name': 'Dev', 'icon': 'code'}
      ],
    })!;
    expect(s.revision, 5);
    expect(s.active!.controls.length, 2);
    expect(s.active!.controls.first.available, isFalse);
    expect(s.active!.controls.first.unavailableReason, 'Not configured.');
    expect(s.active!.controls.last.widget, WidgetKind.volume);
    expect(WorkspaceState.tryParse({'mode': 'auto'}), isNull);
  });

  test('telemetry keeps unavailable values null', () {
    final t = Telemetry.tryParse({'cpuPercent': 12.5, 'ramPercent': 40})!;
    expect(t.cpu, 12.5);
    expect(t.volume, isNull);
    expect(t.micMuted, isNull);
  });
}
