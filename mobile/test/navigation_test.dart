import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:skca_enrol/api/api_client.dart';
import 'package:skca_enrol/main.dart';
import 'package:skca_enrol/state/auth_state.dart';
import 'package:skca_enrol/state/token_storage.dart';

/// Runs the whole app (real router + screens) against a fake API.
Future<(AuthState, InMemoryTokenStorage)> pumpApp(WidgetTester tester, {String role = 'Parent'}) async {
  final api = ApiClient(MockClient((request) async {
    if (request.url.path == '/api/auth/login') {
      return http.Response(
          jsonEncode({
            'token': 'jwt-token',
            'expiresAt': DateTime.now().add(const Duration(hours: 8)).toIso8601String(),
            'user': {'id': 3, 'fullName': 'Kumari Fernando', 'email': 'parent.kumari@skca.lk', 'role': role}
          }),
          200);
    }
    if (request.url.path == '/api/enrolments') {
      return http.Response(jsonEncode({'items': [], 'page': 1, 'pageSize': 50, 'totalCount': 0, 'totalPages': 0}), 200);
    }
    return http.Response(jsonEncode([]), 200);
  }), 'http://api.test');

  final storage = InMemoryTokenStorage();
  final auth = AuthState(api, storage);
  await auth.restore(); // nothing saved -> logged out
  await tester.pumpWidget(SkcaApp(api: api, auth: auth));
  await tester.pumpAndSettle();
  return (auth, storage);
}

void main() {
  testWidgets('logged-out users land on the login screen and can open registration', (tester) async {
    await pumpApp(tester);

    expect(find.text('Sign in'), findsOneWidget);
    await tester.tap(find.text('New here? Create an account'));
    await tester.pumpAndSettle();
    expect(find.text('Create a parent account'), findsOneWidget);
  });

  testWidgets('logging in opens the protected home screen and stores the token; logging out returns to login',
      (tester) async {
    final (_, storage) = await pumpApp(tester);

    await tester.enterText(find.byKey(const Key('login-email')), 'parent.kumari@skca.lk');
    await tester.enterText(find.byKey(const Key('login-password')), 'Demo@12345');
    await tester.tap(find.text('Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('My enrolments'), findsOneWidget);
    expect(find.textContaining('No enrolment requests yet'), findsOneWidget);
    expect(storage.value, contains('jwt-token'));

    await tester.tap(find.byIcon(Icons.logout));
    await tester.pumpAndSettle();
    expect(find.text('Sign in'), findsOneWidget);
    expect(storage.value, isNull);
  });

  testWidgets('staff accounts are refused in the parent app', (tester) async {
    final (auth, _) = await pumpApp(tester, role: 'Admin');

    await tester.enterText(find.byKey(const Key('login-email')), 'admin@skca.lk');
    await tester.enterText(find.byKey(const Key('login-password')), 'Demo@12345');
    await tester.tap(find.text('Sign in'));
    await tester.pumpAndSettle();

    expect(find.textContaining('This app is for parents'), findsOneWidget);
    expect(auth.isLoggedIn, isFalse);
  });
}
