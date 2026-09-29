import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart';

/// An error the UI can show: HTTP status + the API's ProblemDetails message.
class ApiException implements Exception {
  final int statusCode; // 0 = could not reach the server
  final String message;
  final List<String> fieldErrors;

  ApiException(this.statusCode, this.message, [this.fieldErrors = const []]);

  @override
  String toString() => fieldErrors.isEmpty ? message : '$message\n${fieldErrors.join('\n')}';
}

/// The only class that talks to the ASP.NET Core API.
/// The http.Client is injected so tests can pass a MockClient instead of the real network.
class ApiClient {
  final http.Client _http;
  final String baseUrl;

  /// Returns the current JWT (or null). Set by AuthState after login.
  String? Function() tokenProvider = () => null;

  /// Called when the API answers 401 while we thought we were logged in (token expired).
  void Function()? onUnauthorized;

  static const _timeout = Duration(seconds: 20);

  ApiClient(this._http, this.baseUrl);

  Map<String, String> _headers({bool json = true}) {
    final token = tokenProvider();
    return {
      'Accept': 'application/json',
      if (json) 'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };
  }

  Uri _uri(String path, [Map<String, String>? query]) {
    final uri = Uri.parse('$baseUrl$path');
    return query == null ? uri : uri.replace(queryParameters: {...uri.queryParameters, ...query});
  }

  Future<dynamic> get(String path, {Map<String, String>? query}) =>
      _send(() => _http.get(_uri(path, query), headers: _headers()));

  Future<dynamic> post(String path, [Object? body]) =>
      _send(() => _http.post(_uri(path), headers: _headers(), body: jsonEncode(body ?? {})));

  Future<dynamic> put(String path, Object body) =>
      _send(() => _http.put(_uri(path), headers: _headers(), body: jsonEncode(body)));

  Future<dynamic> delete(String path) => _send(() => _http.delete(_uri(path), headers: _headers()));

  /// Uploads a file as multipart/form-data (used for child photos).
  Future<dynamic> uploadFile(String path, String field, String filePath) {
    return _send(() async {
      final request = http.MultipartRequest('POST', _uri(path))..headers.addAll(_headers(json: false));
      final isPng = filePath.toLowerCase().endsWith('.png');
      request.files.add(await http.MultipartFile.fromPath(
        field,
        filePath,
        contentType: MediaType('image', isPng ? 'png' : 'jpeg'),
      ));
      return http.Response.fromStream(await _http.send(request));
    });
  }

  /// Full URL for images loaded with Image.network (plus the auth header from [authHeaders]).
  String url(String path) => '$baseUrl$path';
  Map<String, String> get authHeaders => _headers(json: false);

  /// Runs the request and turns every failure into an ApiException with a readable message.
  Future<dynamic> _send(Future<http.Response> Function() request) async {
    http.Response response;
    try {
      response = await request().timeout(_timeout);
    } on SocketException {
      throw ApiException(0, 'Cannot reach the server. Check your internet connection.');
    } on http.ClientException {
      throw ApiException(0, 'Cannot reach the server. Check your internet connection.');
    } on TimeoutException {
      throw ApiException(0, 'The server took too long to answer. Please try again.');
    }

    if (response.statusCode == 401 && tokenProvider() != null) onUnauthorized?.call();

    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.body.isEmpty) return null;
      return jsonDecode(response.body);
    }
    throw _toException(response);
  }

  static ApiException _toException(http.Response response) {
    try {
      final problem = jsonDecode(response.body) as Map<String, dynamic>;
      final errors = <String>[];
      if (problem['errors'] is Map) {
        for (final list in (problem['errors'] as Map).values) {
          errors.addAll(List<String>.from(list as List));
        }
      }
      final message = problem['detail'] ?? (errors.isNotEmpty ? 'Please fix the problems below.' : problem['title']);
      return ApiException(response.statusCode, message ?? 'Request failed (${response.statusCode}).', errors);
    } catch (_) {
      return ApiException(response.statusCode, 'Request failed (${response.statusCode}).');
    }
  }
}
