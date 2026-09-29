import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../api/api_client.dart';
import '../models/models.dart';
import 'token_storage.dart';

/// Who is logged in. A ChangeNotifier: when it changes, listening widgets
/// (and the router's redirect) rebuild. This is the Provider pattern (ADR 0002).
class AuthState extends ChangeNotifier {
  final ApiClient _api;
  final TokenStorage _storage;

  AppUser? _user;
  String? _token;
  bool _restoring = true;

  AuthState(this._api, this._storage) {
    // The API client reads the token from here, and tells us when it expires.
    _api.tokenProvider = () => _token;
    _api.onUnauthorized = logout;
  }

  AppUser? get user => _user;
  bool get isLoggedIn => _user != null;

  /// True while we are reading a saved session at app start (show a splash, don't redirect yet).
  bool get isRestoring => _restoring;

  /// Reads a saved session from secure storage when the app starts.
  Future<void> restore() async {
    try {
      final saved = await _storage.read();
      if (saved != null) {
        final json = jsonDecode(saved) as Map<String, dynamic>;
        final expires = DateTime.parse(json['expiresAt']);
        if (expires.isAfter(DateTime.now())) {
          _token = json['token'];
          _user = AppUser.fromJson(json['user']);
        } else {
          await _storage.delete(); // expired: behave as logged out
        }
      }
    } catch (_) {
      await _storage.delete(); // corrupted value: start clean
    }
    _restoring = false;
    notifyListeners();
  }

  Future<void> login(String email, String password) async {
    final json = await _api.post('/api/auth/login', {'email': email.trim(), 'password': password});
    await _accept(AuthResult.fromJson(json));
  }

  /// Public sign-up always creates a Parent account on the server.
  Future<void> register(String fullName, String email, String password) async {
    final json = await _api.post('/api/auth/register', {
      'fullName': fullName.trim(),
      'email': email.trim(),
      'password': password,
    });
    await _accept(AuthResult.fromJson(json));
  }

  Future<void> logout() async {
    _token = null;
    _user = null;
    await _storage.delete();
    notifyListeners();
  }

  Future<void> _accept(AuthResult result) async {
    // This app is for parents only; staff use the web app.
    if (result.user.role != 'Parent') {
      throw ApiException(403, 'This app is for parents. Staff please use the SKCA Enrol website.');
    }
    _token = result.token;
    _user = result.user;
    await _storage.write(jsonEncode({
      'token': result.token,
      'expiresAt': result.expiresAt.toIso8601String(),
      'user': result.user.toJson(),
    }));
    notifyListeners();
  }
}
