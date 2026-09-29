import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../state/auth_state.dart';
import '../state/children_state.dart';
import '../state/enrolments_state.dart';

/// App bar with logout + bottom navigation between "My enrolments" and "My children".
class HomeShell extends StatelessWidget {
  final String location;
  final Widget child;
  const HomeShell({super.key, required this.location, required this.child});

  @override
  Widget build(BuildContext context) {
    final onChildren = location.startsWith('/children');
    final user = context.watch<AuthState>().user;

    return Scaffold(
      appBar: AppBar(
        title: Text(onChildren ? 'My children' : 'My enrolments'),
        actions: [
          IconButton(
            tooltip: 'Log out${user == null ? '' : ' (${user.fullName})'}',
            icon: const Icon(Icons.logout),
            onPressed: () async {
              // Forget this parent's data so the next user never sees it.
              context.read<ChildrenState>().clear();
              context.read<EnrolmentsState>().clear();
              await context.read<AuthState>().logout(); // the router then redirects to /login
            },
          ),
        ],
      ),
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: onChildren ? 1 : 0,
        onDestinationSelected: (i) => context.go(i == 0 ? '/enrolments' : '/children'),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.assignment_outlined), selectedIcon: Icon(Icons.assignment), label: 'Enrolments'),
          NavigationDestination(icon: Icon(Icons.child_care_outlined), selectedIcon: Icon(Icons.child_care), label: 'Children'),
        ],
      ),
    );
  }
}
