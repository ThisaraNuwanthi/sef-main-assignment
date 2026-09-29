import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';
import 'package:skca_enrol/api/api_client.dart';
import 'package:skca_enrol/screens/enrolment_form_screen.dart';
import 'package:skca_enrol/screens/validators.dart';
import 'package:skca_enrol/state/children_state.dart';
import 'package:skca_enrol/state/enrolments_state.dart';

void main() {
  late List<http.Request> requests;

  /// Builds the form screen with a fake API that knows one child.
  Future<void> pumpForm(WidgetTester tester) async {
    requests = [];
    final api = ApiClient(MockClient((request) async {
      requests.add(request);
      if (request.method == 'GET' && request.url.path == '/api/children') {
        return http.Response(
            jsonEncode([
              {'id': 5, 'fullName': 'Kavindu Fernando', 'dateOfBirth': '2017-05-10', 'age': 9, 'lichessUsername': null, 'hasPhoto': false}
            ]),
            200);
      }
      if (request.method == 'POST' && request.url.path == '/api/enrolments') {
        return http.Response(jsonEncode({'enrolmentId': 42, 'workflowId': 7, 'status': 'Submitted'}), 201);
      }
      return http.Response(jsonEncode({'items': []}), 200);
    }), 'http://api.test');

    final router = GoRouter(initialLocation: '/new', routes: [
      GoRoute(path: '/new', builder: (_, _) => const EnrolmentFormScreen()),
      GoRoute(path: '/enrolments/:id', builder: (_, s) => Scaffold(body: Text('Detail ${s.pathParameters['id']}'))),
    ]);

    await tester.pumpWidget(MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => ChildrenState(api)),
        ChangeNotifierProvider(create: (_) => EnrolmentsState(api)),
      ],
      child: MaterialApp.router(routerConfig: router),
    ));
    await tester.pumpAndSettle(); // children load
  }

  testWidgets('shows validation errors and sends nothing when child and days are missing', (tester) async {
    await pumpForm(tester);

    await tester.tap(find.byKey(const Key('submit-enrolment')));
    await tester.pumpAndSettle();

    expect(find.text('Choose a child.'), findsOneWidget);
    expect(find.text('Choose at least one day.'), findsOneWidget);
    expect(requests.where((r) => r.method == 'POST'), isEmpty);
  });

  testWidgets('a valid form posts the request and opens its detail screen', (tester) async {
    await pumpForm(tester);

    await tester.tap(find.byKey(const Key('child-dropdown')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Kavindu Fernando (age 9)').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sat'));
    await tester.enterText(find.byType(TextFormField).last, 'Loves puzzles');
    await tester.ensureVisible(find.byKey(const Key('submit-enrolment')));
    await tester.tap(find.byKey(const Key('submit-enrolment')));
    await tester.pumpAndSettle();

    final post = requests.singleWhere((r) => r.method == 'POST');
    final body = jsonDecode(post.body) as Map<String, dynamic>;
    expect(body['childId'], 5);
    expect(body['preferredDays'], ['Saturday']);
    expect(body['parentNotes'], 'Loves puzzles');
    expect(find.text('Detail 42'), findsOneWidget);
  });

  test('time range validator: "to" must be after "from"', () {
    expect(validateTimeRange('14:00', '18:00'), isNull);
    expect(validateTimeRange('18:00', '14:00'), isNotNull);
    expect(validateTimeRange('14:00', '14:00'), isNotNull);
    expect(validateTimeRange(null, '14:00'), isNull); // one side open is fine
  });

  test('date of birth must give an age of 4 to 18', () {
    final today = DateTime(2026, 9, 29);
    expect(validateDateOfBirth(DateTime(2017, 5, 10), today: today), isNull);
    expect(validateDateOfBirth(DateTime(2024, 1, 1), today: today), isNotNull); // age 2
    expect(validateDateOfBirth(null), isNotNull);
  });
}
