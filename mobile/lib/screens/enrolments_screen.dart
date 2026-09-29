import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../state/enrolments_state.dart';
import '../widgets/common.dart';

/// "My enrolments": every request with its status, a search box and status filter chips.
class EnrolmentsScreen extends StatefulWidget {
  const EnrolmentsScreen({super.key});

  @override
  State<EnrolmentsScreen> createState() => _EnrolmentsScreenState();
}

class _EnrolmentsScreenState extends State<EnrolmentsScreen> {
  final _search = TextEditingController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final state = context.read<EnrolmentsState>();
      _search.text = state.search;
      state.load();
    });
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<EnrolmentsState>();

    return Scaffold(
      body: Column(children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 12, 12, 4),
          child: TextField(
            controller: _search,
            decoration: InputDecoration(
              labelText: 'Search by child or class',
              prefixIcon: const Icon(Icons.search),
              border: const OutlineInputBorder(),
              isDense: true,
              suffixIcon: _search.text.isEmpty
                  ? null
                  : IconButton(
                      tooltip: 'Clear search',
                      icon: const Icon(Icons.clear),
                      onPressed: () {
                        _search.clear();
                        state.setFilter(search: '');
                      },
                    ),
            ),
            textInputAction: TextInputAction.search,
            onSubmitted: (v) => state.setFilter(search: v),
          ),
        ),
        // Status filter: one chip per status, plus "All".
        SizedBox(
          height: 48,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 12),
            children: [
              _chip(state, null, 'All'),
              for (final s in enrolmentStatuses) _chip(state, s, humanize(s)),
            ],
          ),
        ),
        Expanded(child: _list(state)),
      ]),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('/enrolments/new'),
        icon: const Icon(Icons.add),
        label: const Text('New request'),
      ),
    );
  }

  Widget _chip(EnrolmentsState state, String? status, String label) => Padding(
        padding: const EdgeInsets.only(right: 6),
        child: ChoiceChip(
          label: Text(label),
          selected: state.statusFilter == status,
          onSelected: (_) => state.setFilter(status: status, clearStatus: status == null),
        ),
      );

  Widget _list(EnrolmentsState state) {
    if (state.loading && state.items.isEmpty) return const LoadingView(label: 'Loading your requests…');
    if (state.error != null && state.items.isEmpty) return ErrorView(error: state.error!, onRetry: state.load);
    if (state.items.isEmpty) {
      final filtered = state.statusFilter != null || state.search.isNotEmpty;
      return EmptyView(
        icon: Icons.assignment_outlined,
        message: filtered ? 'No requests match this filter.' : 'No enrolment requests yet.\nTap "New request" to place your child in a class.',
      );
    }
    return RefreshIndicator(
      onRefresh: state.load,
      child: ListView.builder(
        padding: const EdgeInsets.only(bottom: 88),
        itemCount: state.items.length,
        itemBuilder: (context, i) {
          final e = state.items[i];
          final className = e.assignedClassName ?? e.requestedClassName;
          return Card(
            margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            child: ListTile(
              title: Text(e.childName),
              subtitle: Text([
                ?className,
                'Requested ${DateFormat.yMMMd().format(e.createdAt.toLocal())}',
              ].join(' · ')),
              trailing: StatusChip(e.status),
              onTap: () => context.push('/enrolments/${e.id}'),
            ),
          );
        },
      ),
    );
  }
}
