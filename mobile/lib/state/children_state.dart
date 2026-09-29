import 'package:flutter/foundation.dart';

import '../api/api_client.dart';
import '../models/models.dart';

/// The parent's children, shared by the Children screen and the enrolment form's dropdown.
class ChildrenState extends ChangeNotifier {
  final ApiClient _api;
  ChildrenState(this._api);

  List<Child> children = [];
  bool loading = false;
  Object? error;
  bool _loadedOnce = false;

  bool get loadedOnce => _loadedOnce;

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      final json = await _api.get('/api/children') as List;
      children = json.map((c) => Child.fromJson(c)).toList();
      _loadedOnce = true;
    } catch (e) {
      error = e;
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  /// Creates or updates a child. Returns the saved child (so a photo can be uploaded next).
  Future<Child> save({int? id, required String fullName, required DateTime dateOfBirth, String? lichessUsername}) async {
    final body = {
      'fullName': fullName.trim(),
      'dateOfBirth': _dateOnly(dateOfBirth),
      'lichessUsername': (lichessUsername == null || lichessUsername.trim().isEmpty) ? null : lichessUsername.trim(),
    };
    final json = id == null ? await _api.post('/api/children', body) : await _api.put('/api/children/$id', body);
    final saved = Child.fromJson(json);
    await load();
    return saved;
  }

  Future<void> uploadPhoto(int childId, String filePath) async {
    await _api.uploadFile('/api/children/$childId/photo', 'photo', filePath);
    await load();
  }

  Future<void> delete(int childId) async {
    await _api.delete('/api/children/$childId');
    await load();
  }

  String photoUrl(int childId) => _api.url('/api/children/$childId/photo');
  Map<String, String> get photoHeaders => _api.authHeaders;

  void clear() {
    children = [];
    _loadedOnce = false;
    notifyListeners();
  }

  static String _dateOnly(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
}
