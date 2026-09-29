import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../state/children_state.dart';
import '../state/enrolments_state.dart';
import '../widgets/common.dart';
import 'validators.dart';

/// New enrolment request (or edit one after the admin asked for a revision).
class EnrolmentFormScreen extends StatefulWidget {
  final int? enrolmentId; // set when editing
  final int? initialChildId; // set when coming from a child's "Enrol" button
  const EnrolmentFormScreen({super.key, this.enrolmentId, this.initialChildId});

  @override
  State<EnrolmentFormScreen> createState() => _EnrolmentFormScreenState();
}

class _EnrolmentFormScreenState extends State<EnrolmentFormScreen> {
  final _formKey = GlobalKey<FormState>();
  final _notes = TextEditingController();
  int? _childId;
  final Set<String> _days = {};
  TimeOfDay? _from;
  TimeOfDay? _to;
  String? _timeError;
  String? _revisionNote; // what the admin asked to change
  bool _loading = false;
  bool _busy = false;
  Object? _error;

  bool get _isEdit => widget.enrolmentId != null;

  @override
  void initState() {
    super.initState();
    _childId = widget.initialChildId;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final children = context.read<ChildrenState>();
      if (!children.loadedOnce) children.load();
      if (_isEdit) _loadExisting();
    });
  }

  Future<void> _loadExisting() async {
    setState(() => _loading = true);
    try {
      final d = await context.read<EnrolmentsState>().detail(widget.enrolmentId!);
      setState(() {
        _childId = d.childId;
        _days.addAll(d.preferredDays);
        _from = _parse(d.preferredTimeFrom);
        _to = _parse(d.preferredTimeTo);
        _notes.text = d.parentNotes ?? '';
        _revisionNote = d.status == 'RevisionRequested' ? d.latestDecisionNote : null;
      });
    } catch (e) {
      setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  static TimeOfDay? _parse(String? hhmm) =>
      hhmm == null ? null : TimeOfDay(hour: int.parse(hhmm.substring(0, 2)), minute: int.parse(hhmm.substring(3, 5)));

  @override
  void dispose() {
    _notes.dispose();
    super.dispose();
  }

  Future<void> _pickTime(bool isFrom) async {
    final picked = await showTimePicker(
      context: context,
      initialTime: (isFrom ? _from : _to) ?? TimeOfDay(hour: isFrom ? 14 : 18, minute: 0),
      helpText: isFrom ? 'Earliest start time' : 'Latest end time',
    );
    if (picked != null) setState(() => isFrom ? _from = picked : _to = picked);
  }

  Future<void> _submit() async {
    final formOk = _formKey.currentState!.validate();
    final timeError = validateTimeRange(_from == null ? null : formatTime(_from!), _to == null ? null : formatTime(_to!));
    setState(() => _timeError = timeError);
    if (!formOk || timeError != null) return;

    setState(() {
      _busy = true;
      _error = null;
    });
    final state = context.read<EnrolmentsState>();
    // Send days in week order so the request reads naturally.
    final days = weekDays.where(_days.contains).toList();
    final prefs = EnrolmentsState.preferences(
      days: days,
      timeFrom: _from == null ? null : formatTime(_from!),
      timeTo: _to == null ? null : formatTime(_to!),
      notes: _notes.text,
    );
    try {
      if (_isEdit) {
        await state.update(widget.enrolmentId!, prefs);
        if (!mounted) return;
        showSuccess(context, 'Request updated and sent again.');
        context.pop();
      } else {
        final id = await state.create(childId: _childId!, preferences: prefs);
        if (!mounted) return;
        showSuccess(context, 'Request sent! We are finding the best class.');
        context.pushReplacement('/enrolments/$id');
      }
    } catch (e) {
      setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final children = context.watch<ChildrenState>();

    return Scaffold(
      appBar: AppBar(title: Text(_isEdit ? 'Edit request' : 'New enrolment request')),
      body: SafeArea(
        child: _loading
            ? const LoadingView()
            : SingleChildScrollView(
                padding: const EdgeInsets.all(20),
                child: Form(
                  key: _formKey,
                  child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                    if (_error != null) ErrorBanner(error: _error!),
                    if (_revisionNote != null)
                      Card(
                        color: Colors.orange.shade50,
                        child: ListTile(
                          leading: const Icon(Icons.info_outline),
                          title: const Text('The academy asked for a change'),
                          subtitle: Text(_revisionNote!),
                        ),
                      ),

                    // ----- Child -----
                    if (children.loading && children.children.isEmpty)
                      const LinearProgressIndicator()
                    else if (children.children.isEmpty && !_isEdit)
                      EmptyView(
                        icon: Icons.child_care,
                        message: 'Add a child first.',
                        action: TextButton(onPressed: () => context.push('/children/new'), child: const Text('Add a child')),
                      )
                    else
                      DropdownButtonFormField<int>(
                        key: const Key('child-dropdown'),
                        initialValue: children.children.any((c) => c.id == _childId) ? _childId : null,
                        decoration: const InputDecoration(labelText: 'Child', border: OutlineInputBorder()),
                        items: [
                          for (final c in children.children) DropdownMenuItem(value: c.id, child: Text('${c.fullName} (age ${c.age})')),
                        ],
                        onChanged: _isEdit ? null : (v) => setState(() => _childId = v), // the child can't change on edit
                        validator: (v) => v == null ? 'Choose a child.' : null,
                      ),
                    const SizedBox(height: 20),

                    // ----- Preferred days (a FormField so it can show a validation error) -----
                    FormField<Set<String>>(
                      initialValue: _days,
                      validator: (_) => _days.isEmpty ? 'Choose at least one day.' : null,
                      builder: (field) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text('Preferred days', style: Theme.of(context).textTheme.titleSmall),
                        const SizedBox(height: 6),
                        Wrap(spacing: 6, runSpacing: 6, children: [
                          for (final day in weekDays)
                            FilterChip(
                              label: Text(day.substring(0, 3)),
                              tooltip: day,
                              selected: _days.contains(day),
                              onSelected: (on) {
                                setState(() => on ? _days.add(day) : _days.remove(day));
                                field.didChange(_days);
                              },
                            ),
                        ]),
                        if (field.hasError)
                          Padding(
                            padding: const EdgeInsets.only(top: 6),
                            child: Text(field.errorText!, style: TextStyle(color: Theme.of(context).colorScheme.error, fontSize: 12)),
                          ),
                      ]),
                    ),
                    const SizedBox(height: 20),

                    // ----- Time window -----
                    Text('Preferred time (optional)', style: Theme.of(context).textTheme.titleSmall),
                    const SizedBox(height: 6),
                    Row(children: [
                      Expanded(child: _timeButton('From', _from, () => _pickTime(true), () => setState(() => _from = null))),
                      const SizedBox(width: 12),
                      Expanded(child: _timeButton('To', _to, () => _pickTime(false), () => setState(() => _to = null))),
                    ]),
                    if (_timeError != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 6),
                        child: Text(_timeError!, style: TextStyle(color: Theme.of(context).colorScheme.error, fontSize: 12)),
                      ),
                    const SizedBox(height: 20),

                    // ----- Notes -----
                    TextFormField(
                      controller: _notes,
                      maxLength: 500,
                      maxLines: 4,
                      decoration: const InputDecoration(
                        labelText: 'Notes for the academy (optional)',
                        hintText: 'e.g. He enjoys puzzles and is a bit shy at first.',
                        border: OutlineInputBorder(),
                      ),
                    ),
                    const SizedBox(height: 12),
                    FilledButton(
                      key: const Key('submit-enrolment'),
                      onPressed: _busy ? null : _submit,
                      child: Text(_busy ? 'Sending…' : _isEdit ? 'Save and resend' : 'Send request'),
                    ),
                  ]),
                ),
              ),
      ),
    );
  }

  Widget _timeButton(String label, TimeOfDay? value, VoidCallback onPick, VoidCallback onClear) => InputDecorator(
        decoration: InputDecoration(
          labelText: label,
          border: const OutlineInputBorder(),
          suffixIcon: value == null ? null : IconButton(tooltip: 'Clear $label time', icon: const Icon(Icons.clear), onPressed: onClear),
        ),
        child: InkWell(onTap: onPick, child: Text(value == null ? 'Any time' : formatTime(value))),
      );
}
