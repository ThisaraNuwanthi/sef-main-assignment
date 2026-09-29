import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'screens/child_form_screen.dart';
import 'screens/children_screen.dart';
import 'screens/enrolment_detail_screen.dart';
import 'screens/enrolment_form_screen.dart';
import 'screens/enrolments_screen.dart';
import 'screens/home_shell.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'state/auth_state.dart';

/// All screens and the rule for protected screens.
GoRouter createRouter(AuthState auth, {String initialLocation = '/enrolments'}) {
  const publicPaths = {'/login', '/register'};

  return GoRouter(
    initialLocation: initialLocation,
    refreshListenable: auth, // re-run redirect whenever login state changes
    redirect: (context, state) {
      final path = state.matchedLocation;
      if (auth.isRestoring) return path == '/splash' ? null : '/splash';
      final isPublic = publicPaths.contains(path);
      // Protected screens need a login; logged-in users skip the login/register/splash screens.
      if (!auth.isLoggedIn) return isPublic ? null : '/login';
      if (isPublic || path == '/splash') return '/enrolments';
      return null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const Scaffold(body: Center(child: CircularProgressIndicator()))),
      GoRoute(path: '/login', builder: (_, _) => const LoginScreen()),
      GoRoute(path: '/register', builder: (_, _) => const RegisterScreen()),

      // The two main tabs share a bottom navigation bar.
      ShellRoute(
        builder: (context, state, child) => HomeShell(location: state.matchedLocation, child: child),
        routes: [
          GoRoute(path: '/enrolments', builder: (_, _) => const EnrolmentsScreen()),
          GoRoute(path: '/children', builder: (_, _) => const ChildrenScreen()),
        ],
      ),

      GoRoute(path: '/children/new', builder: (_, _) => const ChildFormScreen()),
      GoRoute(
        path: '/children/:id/edit',
        builder: (_, state) => ChildFormScreen(childId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/enrolments/new',
        builder: (_, state) => EnrolmentFormScreen(initialChildId: int.tryParse(state.uri.queryParameters['childId'] ?? '')),
      ),
      GoRoute(
        path: '/enrolments/:id',
        builder: (_, state) => EnrolmentDetailScreen(enrolmentId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/enrolments/:id/edit',
        builder: (_, state) => EnrolmentFormScreen(enrolmentId: int.parse(state.pathParameters['id']!)),
      ),
    ],
  );
}
