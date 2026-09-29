import 'dart:io';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../state/children_state.dart';
import '../widgets/common.dart';
import 'validators.dart';

/// Add or edit a child, including an optional photo (camera or gallery).
class ChildFormScreen extends StatefulWidget {
  final int? childId; // null = add a new child
  const ChildFormScreen({super.key, this.childId});

  @override
  State<ChildFormScreen> createState() => _ChildFormScreenState();
}

class _ChildFormScreenState extends State<ChildFormScreen> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _lichess = TextEditingController();
  DateTime? _dob;
  String? _dobError;
  XFile? _photo;
  Child? _existing;
  bool _busy = false;
  Object? _error;

  bool get _isEdit => widget.childId != null;

  @override
  void initState() {
    super.initState();
    if (_isEdit) {
      _existing = context.read<ChildrenState>().children.where((c) => c.id == widget.childId).firstOrNull;
      if (_existing != null) {
        _name.text = _existing!.fullName;
        _lichess.text = _existing!.lichessUsername ?? '';
        _dob = _existing!.dateOfBirth;
      }
    }
  }

  @override
  void dispose() {
    _name.dispose();
    _lichess.dispose();
    super.dispose();
  }

  Future<void> _pickDob() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _dob ?? DateTime(now.year - 8, now.month, now.day),
      firstDate: DateTime(now.year - 19),
      lastDate: now,
      helpText: 'Date of birth',
    );
    if (picked != null) setState(() => _dob = picked);
  }

  Future<void> _pickPhoto(ImageSource source) async {
    // Resize and compress on the phone so uploads stay well under the API's 2 MB limit.
    final file = await ImagePicker().pickImage(source: source, maxWidth: 1024, imageQuality: 80);
    if (file != null) setState(() => _photo = file);
  }

  Future<void> _save() async {
    final formOk = _formKey.currentState!.validate();
    setState(() => _dobError = validateDateOfBirth(_dob));
    if (!formOk || _dobError != null) return;

    setState(() {
      _busy = true;
      _error = null;
    });
    final state = context.read<ChildrenState>();
    try {
      final saved = await state.save(id: widget.childId, fullName: _name.text, dateOfBirth: _dob!, lichessUsername: _lichess.text);
      if (_photo != null) await state.uploadPhoto(saved.id, _photo!.path);
      if (!mounted) return;
      showSuccess(context, '${saved.fullName} saved.');
      context.pop();
    } catch (e) {
      setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _delete() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Remove child?'),
        content: Text('Remove ${_existing?.fullName}? Children with enrolments cannot be removed.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Keep')),
          TextButton(onPressed: () => Navigator.pop(c, true), child: const Text('Remove')),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    try {
      await context.read<ChildrenState>().delete(widget.childId!);
      if (mounted) context.pop();
    } catch (e) {
      setState(() => _error = e);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_isEdit ? 'Edit child' : 'Add a child'),
        actions: [
          if (_isEdit) IconButton(tooltip: 'Remove child', icon: const Icon(Icons.delete_outline), onPressed: _busy ? null : _delete),
        ],
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formKey,
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              if (_error != null) ErrorBanner(error: _error!),
              Center(child: _photoPreview()),
              Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                TextButton.icon(onPressed: () => _pickPhoto(ImageSource.camera), icon: const Icon(Icons.photo_camera), label: const Text('Camera')),
                TextButton.icon(onPressed: () => _pickPhoto(ImageSource.gallery), icon: const Icon(Icons.photo_library), label: const Text('Gallery')),
              ]),
              const SizedBox(height: 8),
              TextFormField(
                controller: _name,
                decoration: const InputDecoration(labelText: "Child's full name", border: OutlineInputBorder()),
                textCapitalization: TextCapitalization.words,
                validator: validateName,
              ),
              const SizedBox(height: 12),
              // Date of birth: a button that opens the date picker, with its own error text.
              InputDecorator(
                decoration: InputDecoration(labelText: 'Date of birth', border: const OutlineInputBorder(), errorText: _dobError),
                child: InkWell(
                  onTap: _pickDob,
                  child: Row(children: [
                    Expanded(child: Text(_dob == null ? 'Tap to choose' : DateFormat.yMMMd().format(_dob!))),
                    const Icon(Icons.calendar_today, size: 18),
                  ]),
                ),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _lichess,
                decoration: const InputDecoration(
                  labelText: 'Lichess username (optional)',
                  helperText: 'Helps us assess their level from online games',
                  border: OutlineInputBorder(),
                ),
                validator: validateLichess,
              ),
              const SizedBox(height: 20),
              FilledButton(onPressed: _busy ? null : _save, child: Text(_busy ? 'Saving…' : 'Save')),
            ]),
          ),
        ),
      ),
    );
  }

  Widget _photoPreview() {
    if (_photo != null) {
      return CircleAvatar(radius: 48, backgroundImage: FileImage(File(_photo!.path)));
    }
    if (_existing?.hasPhoto == true) {
      final state = context.read<ChildrenState>();
      return CircleAvatar(radius: 48, backgroundImage: NetworkImage(state.photoUrl(_existing!.id), headers: state.photoHeaders));
    }
    return const CircleAvatar(radius: 48, child: Icon(Icons.person, size: 48));
  }
}
