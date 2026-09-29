import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../state/enrolments_state.dart';
import '../widgets/common.dart';

/// One request: what's happening now, the approved class and fee, and the full status timeline.
class EnrolmentDetailScreen extends StatefulWidget {
  final int enrolmentId;
  const EnrolmentDetailScreen({super.key, required this.enrolmentId});

  @override
  State<EnrolmentDetailScreen> createState() => _EnrolmentDetailScreenState();
}

class _EnrolmentDetailScreenState extends State<EnrolmentDetailScreen> {
  EnrolmentDetail? _detail;
  List<StatusHistoryEntry> _history = [];
  Object? _error;
  bool _loading = true;
  Timer? _poll;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    final state = context.read<EnrolmentsState>();
    try {
      // Detail and history are independent, so fetch them at the same time.
      final results = await Future.wait([state.detail(widget.enrolmentId), state.history(widget.enrolmentId)]);
      if (!mounted) return;
      setState(() {
        _detail = results[0] as EnrolmentDetail;
        _history = results[1] as List<StatusHistoryEntry>;
        _error = null;
      });
      // While the agents are working, check again every 3 seconds; stop once it moves on.
      final working = ['Submitted', 'AgentProcessing'].contains(_detail!.status);
      if (working && _poll == null) {
        _poll = Timer.periodic(const Duration(seconds: 3), (_) => _load());
      } else if (!working) {
        _poll?.cancel();
        _poll = null;
      }
    } catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _cancel() async {
    final reason = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Cancel this request?'),
        content: TextField(controller: reason, decoration: const InputDecoration(labelText: 'Reason (optional)')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Keep it')),
          TextButton(onPressed: () => Navigator.pop(c, true), child: const Text('Cancel request')),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    try {
      await context.read<EnrolmentsState>().cancel(widget.enrolmentId, reason.text.trim().isEmpty ? null : reason.text.trim());
      if (mounted) showSuccess(context, 'Request cancelled.');
      await _load();
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final d = _detail;
    return Scaffold(
      appBar: AppBar(title: Text(d?.childName ?? 'Request')),
      body: _loading
          ? const LoadingView()
          : d == null
              ? ErrorView(error: _error ?? 'Not found', onRetry: _load)
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(padding: const EdgeInsets.all(16), children: [
                    if (_error != null) ErrorBanner(error: _error!),
                    _statusCard(d),
                    if (d.assignedClass != null) _classCard(d),
                    _preferencesCard(d),
                    _timeline(),
                    const SizedBox(height: 12),
                    if (d.canEdit)
                      FilledButton.icon(
                        onPressed: () async {
                          await context.push('/enrolments/${d.id}/edit');
                          _load();
                        },
                        icon: const Icon(Icons.edit),
                        label: const Text('Edit request'),
                      ),
                    if (d.canCancel)
                      TextButton.icon(onPressed: _cancel, icon: const Icon(Icons.cancel_outlined), label: const Text('Cancel request')),
                  ]),
                ),
    );
  }

  /// A plain-language explanation of the current status.
  Widget _statusCard(EnrolmentDetail d) {
    final (icon, text) = switch (d.status) {
      'Submitted' || 'AgentProcessing' => (Icons.hourglass_top, 'Our placement assistant is checking levels, times and seats…'),
      'PendingAdminApproval' => (Icons.how_to_reg, 'A class has been suggested. The academy is reviewing it now.'),
      'Approved' => (Icons.check_circle, 'Confirmed! Your child has a place.'),
      'RevisionRequested' => (Icons.edit_note, 'The academy asked you to change something:\n${d.latestDecisionNote ?? ''}'),
      'Rejected' => (Icons.block, 'This request was not accepted.${d.latestDecisionNote == null ? '' : '\n${d.latestDecisionNote}'}'),
      'Failed' => (Icons.support_agent, "We couldn't find a suitable class automatically. The academy will look at it."),
      _ => (Icons.cancel, 'This request was cancelled.'),
    };
    final color = StatusChip.colorFor(d.status);
    return Card(
      child: ListTile(
        leading: Icon(icon, color: color, size: 32),
        title: Row(children: [StatusChip(d.status)]),
        subtitle: Padding(padding: const EdgeInsets.only(top: 8), child: Text(text)),
        trailing: ['Submitted', 'AgentProcessing'].contains(d.status)
            ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
            : null,
      ),
    );
  }

  Widget _classCard(EnrolmentDetail d) {
    final c = d.assignedClass!;
    final fee = d.fees.isEmpty ? null : d.fees.first;
    final money = NumberFormat.currency(locale: 'en_LK', symbol: 'LKR ');
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('Your class', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          Text(c.name, style: Theme.of(context).textTheme.titleLarge),
          Text('${c.level} · ${c.dayOfWeek}s ${c.startTime}–${c.endTime}'),
          if (c.coachName.isNotEmpty) Text('Coach: ${c.coachName}'),
          if (fee != null) ...[
            const Divider(height: 24),
            Text('Monthly fee: ${money.format(fee.amount)}', style: const TextStyle(fontWeight: FontWeight.w600)),
            if (fee.siblingDiscountApplied) const Text('Includes a 10% sibling discount'),
            Text('${DateFormat.yMMMM().format(fee.month)}: ${humanize(fee.status)}'),
          ],
        ]),
      ),
    );
  }

  Widget _preferencesCard(EnrolmentDetail d) {
    final time = (d.preferredTimeFrom == null && d.preferredTimeTo == null)
        ? 'Any time'
        : '${d.preferredTimeFrom ?? 'any'} – ${d.preferredTimeTo ?? 'any'}';
    return Card(
      child: ListTile(
        title: const Text('What you asked for'),
        subtitle: Text('${d.preferredDays.join(', ')}\n$time${d.parentNotes == null ? '' : '\nNotes: ${d.parentNotes}'}'),
        isThreeLine: true,
      ),
    );
  }

  /// Status history as a vertical timeline, oldest first.
  Widget _timeline() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('History', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          for (var i = 0; i < _history.length; i++)
            Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Column(children: [
                Icon(Icons.circle, size: 12, color: StatusChip.colorFor(_history[i].toStatus)),
                if (i < _history.length - 1) Container(width: 2, height: 44, color: Colors.grey.shade300),
              ]),
              const SizedBox(width: 12),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(humanize(_history[i].toStatus), style: const TextStyle(fontWeight: FontWeight.w600)),
                  Text(
                    '${DateFormat.yMMMd().add_jm().format(_history[i].changedAt.toLocal())} · ${_history[i].changedBy}',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                  if (_history[i].note != null) Text(_history[i].note!, style: Theme.of(context).textTheme.bodySmall),
                  const SizedBox(height: 8),
                ]),
              ),
            ]),
        ]),
      ),
    );
  }
}
