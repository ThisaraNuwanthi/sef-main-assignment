import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import 'api/api_client.dart';
import 'config.dart';
import 'router.dart';
import 'state/auth_state.dart';
import 'state/children_state.dart';
import 'state/enrolments_state.dart';
import 'state/token_storage.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  final api = ApiClient(http.Client(), AppConfig.apiBaseUrl);
  final auth = AuthState(api, SecureTokenStorage())..restore();
  runApp(SkcaApp(api: api, auth: auth));
}

/// Root widget. Tests build it with a mock ApiClient and in-memory token storage.
class SkcaApp extends StatefulWidget {
  final ApiClient api;
  final AuthState auth;
  const SkcaApp({super.key, required this.api, required this.auth});

  @override
  State<SkcaApp> createState() => _SkcaAppState();
}

class _SkcaAppState extends State<SkcaApp> {
  late final GoRouter _router = createRouter(widget.auth); // created once, not on every rebuild

  @override
  Widget build(BuildContext context) {
    // Provider makes these objects available to every screen below (context.watch / context.read).
    return MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: widget.auth),
        ChangeNotifierProvider(create: (_) => ChildrenState(widget.api)),
        ChangeNotifierProvider(create: (_) => EnrolmentsState(widget.api)),
      ],
      child: MaterialApp.router(
        title: 'SKCA Enrol',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(colorSchemeSeed: const Color(0xFF2F6F4F), useMaterial3: true),
        routerConfig: _router,
      ),
    );
  }
}
