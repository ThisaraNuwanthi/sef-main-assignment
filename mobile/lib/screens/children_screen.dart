import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../state/children_state.dart';
import '../widgets/common.dart';

class ChildrenScreen extends StatefulWidget {
  const ChildrenScreen({super.key});

  @override
  State<ChildrenScreen> createState() => _ChildrenScreenState();
}

class _ChildrenScreenState extends State<ChildrenScreen> {
  @override
  void initState() {
    super.initState();
    // Load after the first frame (Provider can't notify listeners during build).
    WidgetsBinding.instance.addPostFrameCallback((_) => context.read<ChildrenState>().load());
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<ChildrenState>();

    Widget body;
    if (state.loading && !state.loadedOnce) {
      body = const LoadingView(label: 'Loading your children…');
    } else if (state.error != null && state.children.isEmpty) {
      body = ErrorView(error: state.error!, onRetry: state.load);
    } else if (state.children.isEmpty) {
      body = EmptyView(
        icon: Icons.child_care,
        message: 'No children yet.\nAdd your child to request a class.',
        action: FilledButton.icon(
          onPressed: () => context.push('/children/new'),
          icon: const Icon(Icons.add),
          label: const Text('Add a child'),
        ),
      );
    } else {
      body = RefreshIndicator(
        onRefresh: state.load,
        child: ListView.separated(
          padding: const EdgeInsets.only(bottom: 88),
          itemCount: state.children.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, i) => _ChildTile(child: state.children[i]),
        ),
      );
    }

    return Scaffold(
      body: body,
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('/children/new'),
        icon: const Icon(Icons.add),
        label: const Text('Add child'),
      ),
    );
  }
}

class _ChildTile extends StatelessWidget {
  final Child child;
  const _ChildTile({required this.child});

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: ChildAvatar(child: child),
      title: Text(child.fullName),
      subtitle: Text('Age ${child.age}${child.lichessUsername == null ? '' : ' · Lichess: ${child.lichessUsername}'}'),
      onTap: () => context.push('/children/${child.id}/edit'),
      trailing: TextButton(
        onPressed: () => context.push('/enrolments/new?childId=${child.id}'),
        child: const Text('Enrol'),
      ),
    );
  }
}

/// Child's photo (loaded through the API with the login token) or their initials.
class ChildAvatar extends StatelessWidget {
  final Child child;
  final double radius;
  const ChildAvatar({super.key, required this.child, this.radius = 22});

  @override
  Widget build(BuildContext context) {
    final initials = child.fullName.split(' ').where((p) => p.isNotEmpty).take(2).map((p) => p[0]).join();
    if (!child.hasPhoto) return CircleAvatar(radius: radius, child: Text(initials));

    final state = context.read<ChildrenState>();
    return CircleAvatar(
      radius: radius,
      backgroundImage: NetworkImage(state.photoUrl(child.id), headers: state.photoHeaders),
      onBackgroundImageError: (_, _) {}, // fall back silently to the plain avatar colour
    );
  }
}
