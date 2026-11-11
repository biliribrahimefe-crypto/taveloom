import 'dart:convert';
import 'dart:math';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class StoredHost {
  StoredHost({
    required this.deviceId,
    required this.credential,
    required this.hosts,
    required this.port,
    required this.fingerprint,
    required this.hostName,
  });

  final String deviceId;
  final String
  credential; // base64url, protected at rest by the Android Keystore
  final List<String> hosts;
  final int port;
  final String fingerprint;
  final String hostName;

  Map<String, dynamic> toJson() => {
    'deviceId': deviceId,
    'credential': credential,
    'hosts': hosts,
    'port': port,
    'fingerprint': fingerprint,
    'hostName': hostName,
  };

  static StoredHost? fromJson(Object? j) {
    if (j is! Map<String, dynamic>) return null;
    final id = j['deviceId'],
        cred = j['credential'],
        fp = j['fingerprint'],
        port = j['port'],
        hosts = j['hosts'];
    if (id is! String ||
        cred is! String ||
        fp is! String ||
        port is! int ||
        hosts is! List) {
      return null;
    }
    return StoredHost(
      deviceId: id,
      credential: cred,
      hosts: hosts.whereType<String>().toList(),
      port: port,
      fingerprint: fp,
      hostName: j['hostName'] as String? ?? 'Windows PC',
    );
  }
}

/// Device identity and the pairing credential. Backed by EncryptedSharedPreferences, whose key lives in the Android Keystore.
class SecureStore {
  SecureStore([FlutterSecureStorage? storage])
    : _s =
          storage ??
          const FlutterSecureStorage(
            aOptions: AndroidOptions(encryptedSharedPreferences: true),
          );

  final FlutterSecureStorage _s;
  static const _hostKey = 'taveloom.host', _idKey = 'taveloom.deviceId';

  Future<StoredHost?> loadHost() async {
    final raw = await _s.read(key: _hostKey);
    if (raw == null) return null;
    try {
      return StoredHost.fromJson(jsonDecode(raw));
    } on FormatException {
      return null;
    }
  }

  Future<void> saveHost(StoredHost h) =>
      _s.write(key: _hostKey, value: jsonEncode(h.toJson()));
  Future<void> clearHost() => _s.delete(key: _hostKey);

  /// Stable random id for this installation. Not derived from any hardware identifier.
  Future<String> deviceId() async {
    var id = await _s.read(key: _idKey);
    if (id == null) {
      final r = Random.secure();
      id =
          'and-${List.generate(16, (_) => r.nextInt(16).toRadixString(16)).join()}';
      await _s.write(key: _idKey, value: id);
    }
    return id;
  }
}
