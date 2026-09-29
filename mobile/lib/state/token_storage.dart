import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Where the JWT is kept between app launches. An interface so tests can use memory instead.
abstract class TokenStorage {
  Future<String?> read();
  Future<void> write(String value);
  Future<void> delete();
}

/// Real storage: Android Keystore-backed encryption via flutter_secure_storage,
/// so other apps (and backups in plain text) cannot read the token.
class SecureTokenStorage implements TokenStorage {
  static const _key = 'skca_session';
  final _storage = const FlutterSecureStorage();

  @override
  Future<String?> read() => _storage.read(key: _key);

  @override
  Future<void> write(String value) => _storage.write(key: _key, value: value);

  @override
  Future<void> delete() => _storage.delete(key: _key);
}

/// For tests only.
class InMemoryTokenStorage implements TokenStorage {
  String? value;
  InMemoryTokenStorage([this.value]);

  @override
  Future<String?> read() async => value;

  @override
  Future<void> write(String v) async => value = v;

  @override
  Future<void> delete() async => value = null;
}
