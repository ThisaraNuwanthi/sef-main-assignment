import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:skca_enrol/api/api_client.dart';

/// Unit tests for ApiClient using http's MockClient (no real network).
void main() {
  test('sends the bearer token and decodes JSON', () async {
    late http.Request seen;
    final api = ApiClient(MockClient((request) async {
      seen = request;
      return http.Response(jsonEncode([{'id': 1}]), 200);
    }), 'http://api.test');
    api.tokenProvider = () => 'abc123';

    final result = await api.get('/api/children');

    expect(seen.url.toString(), 'http://api.test/api/children');
    expect(seen.headers['Authorization'], 'Bearer abc123');
    expect(result, [
      {'id': 1}
    ]);
  });

  test('turns a ProblemDetails 409 into an ApiException with the server message', () async {
    final api = ApiClient(MockClient((_) async {
      return http.Response(jsonEncode({'title': 'Conflict', 'status': 409, 'detail': 'Kavindu already has an open enrolment request.'}), 409);
    }), 'http://api.test');

    expect(
      () => api.post('/api/enrolments', {'childId': 1}),
      throwsA(isA<ApiException>()
          .having((e) => e.statusCode, 'statusCode', 409)
          .having((e) => e.message, 'message', 'Kavindu already has an open enrolment request.')),
    );
  });

  test('collects validation errors from a 400 response', () async {
    final api = ApiClient(MockClient((_) async {
      return http.Response(
          jsonEncode({
            'title': 'One or more validation errors occurred.',
            'errors': {
              'PreferredDays': ['Choose at least one preferred day.']
            }
          }),
          400);
    }), 'http://api.test');

    try {
      await api.post('/api/enrolments', {});
      fail('should have thrown');
    } on ApiException catch (e) {
      expect(e.statusCode, 400);
      expect(e.fieldErrors, ['Choose at least one preferred day.']);
    }
  });

  test('a network failure becomes a friendly ApiException with status 0', () async {
    final api = ApiClient(MockClient((_) async => throw http.ClientException('connection refused')), 'http://api.test');

    expect(() => api.get('/api/children'), throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 0)));
  });

  test('a 401 while logged in triggers onUnauthorized (token expired)', () async {
    var loggedOut = false;
    final api = ApiClient(MockClient((_) async => http.Response('', 401)), 'http://api.test')
      ..tokenProvider = (() => 'expired')
      ..onUnauthorized = () => loggedOut = true;

    await expectLater(api.get('/api/children'), throwsA(isA<ApiException>()));
    expect(loggedOut, isTrue);
  });
}
