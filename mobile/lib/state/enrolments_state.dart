import 'package:flutter/foundation.dart';

import '../api/api_client.dart';
import '../models/models.dart';

/// The parent's enrolment requests, with the search and status filter used by the list screen.
class EnrolmentsState extends ChangeNotifier {
  final ApiClient _api;
  EnrolmentsState(this._api);

  List<EnrolmentListItem> items = [];
  bool loading = false;
  Object? error;

  String search = '';
  String? statusFilter; // null = all statuses

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      // Filtering happens on the server (same endpoint as the admin list; the API only returns our own).
      final json = await _api.get('/api/enrolments', query: {
        'pageSize': '50',
        if (search.trim().isNotEmpty) 'search': search.trim(),
        'status': ?statusFilter,
      });
      items = (json['items'] as List).map((e) => EnrolmentListItem.fromJson(e)).toList();
    } catch (e) {
      error = e;
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<void> setFilter({String? search, String? status, bool clearStatus = false}) {
    if (search != null) this.search = search;
    if (status != null || clearStatus) statusFilter = status;
    return load();
  }

  /// Body shared by create (POST) and edit (PUT).
  static Map<String, dynamic> preferences({
    required List<String> days,
    String? timeFrom,
    String? timeTo,
    String? notes,
  }) =>
      {
        'preferredDays': days,
        'preferredTimeFrom': timeFrom == null ? null : '$timeFrom:00',
        'preferredTimeTo': timeTo == null ? null : '$timeTo:00',
        'parentNotes': (notes == null || notes.trim().isEmpty) ? null : notes.trim(),
      };

  /// Submits a request; the agents start working on it in the background. Returns the enrolment id.
  Future<int> create({required int childId, required Map<String, dynamic> preferences}) async {
    final json = await _api.post('/api/enrolments', {'childId': childId, ...preferences});
    await load();
    return json['enrolmentId'] as int;
  }

  Future<void> update(int id, Map<String, dynamic> preferences) async {
    await _api.put('/api/enrolments/$id', preferences);
    await load();
  }

  Future<void> cancel(int id, String? reason) async {
    await _api.post('/api/enrolments/$id/cancel', {'reason': reason});
    await load();
  }

  Future<EnrolmentDetail> detail(int id) async => EnrolmentDetail.fromJson(await _api.get('/api/enrolments/$id'));

  Future<List<StatusHistoryEntry>> history(int id) async =>
      ((await _api.get('/api/enrolments/$id/history')) as List).map((h) => StatusHistoryEntry.fromJson(h)).toList();

  void clear() {
    items = [];
    search = '';
    statusFilter = null;
    notifyListeners();
  }
}
