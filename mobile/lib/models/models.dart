// Dart versions of the API's JSON (mirrors the C# DTOs). Each has a fromJson factory.

class AppUser {
  final int id;
  final String fullName;
  final String email;
  final String role;

  AppUser({required this.id, required this.fullName, required this.email, required this.role});

  factory AppUser.fromJson(Map<String, dynamic> j) =>
      AppUser(id: j['id'], fullName: j['fullName'], email: j['email'], role: j['role']);

  Map<String, dynamic> toJson() => {'id': id, 'fullName': fullName, 'email': email, 'role': role};
}

class AuthResult {
  final String token;
  final DateTime expiresAt;
  final AppUser user;

  AuthResult({required this.token, required this.expiresAt, required this.user});

  factory AuthResult.fromJson(Map<String, dynamic> j) => AuthResult(
        token: j['token'],
        expiresAt: DateTime.parse(j['expiresAt']),
        user: AppUser.fromJson(j['user']),
      );
}

class Child {
  final int id;
  final String fullName;
  final DateTime dateOfBirth;
  final int age;
  final String? lichessUsername;
  final bool hasPhoto;

  Child({
    required this.id,
    required this.fullName,
    required this.dateOfBirth,
    required this.age,
    this.lichessUsername,
    this.hasPhoto = false,
  });

  factory Child.fromJson(Map<String, dynamic> j) => Child(
        id: j['id'],
        fullName: j['fullName'],
        dateOfBirth: DateTime.parse(j['dateOfBirth']),
        age: j['age'],
        lichessUsername: j['lichessUsername'],
        hasPhoto: j['hasPhoto'] ?? false,
      );
}

/// Every status an enrolment can have, in the order a parent usually sees them.
const enrolmentStatuses = [
  'Submitted',
  'AgentProcessing',
  'PendingAdminApproval',
  'Approved',
  'RevisionRequested',
  'Rejected',
  'Failed',
  'Cancelled',
];

/// Days the way the API names them (System.DayOfWeek).
const weekDays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

class EnrolmentListItem {
  final int id;
  final int childId;
  final String childName;
  final String status;
  final String? requestedClassName;
  final String? assignedClassName;
  final DateTime createdAt;

  EnrolmentListItem({
    required this.id,
    required this.childId,
    required this.childName,
    required this.status,
    this.requestedClassName,
    this.assignedClassName,
    required this.createdAt,
  });

  factory EnrolmentListItem.fromJson(Map<String, dynamic> j) => EnrolmentListItem(
        id: j['id'],
        childId: j['childId'],
        childName: j['childName'],
        status: j['status'],
        requestedClassName: j['requestedClassName'],
        assignedClassName: j['assignedClassName'],
        createdAt: DateTime.parse(j['createdAt']),
      );
}

class ClassSummary {
  final int id;
  final String name;
  final String level;
  final String dayOfWeek;
  final String startTime;
  final String endTime;
  final String coachName;

  ClassSummary({
    required this.id,
    required this.name,
    required this.level,
    required this.dayOfWeek,
    required this.startTime,
    required this.endTime,
    required this.coachName,
  });

  factory ClassSummary.fromJson(Map<String, dynamic> j) => ClassSummary(
        id: j['id'],
        name: j['name'],
        level: j['level'],
        dayOfWeek: j['dayOfWeek'],
        startTime: (j['startTime'] as String).substring(0, 5),
        endTime: (j['endTime'] as String).substring(0, 5),
        coachName: j['coachName'] ?? '',
      );
}

class FeeRecord {
  final DateTime month;
  final double amount;
  final bool siblingDiscountApplied;
  final String status;

  FeeRecord({required this.month, required this.amount, required this.siblingDiscountApplied, required this.status});

  factory FeeRecord.fromJson(Map<String, dynamic> j) => FeeRecord(
        month: DateTime.parse(j['month']),
        amount: (j['amount'] as num).toDouble(),
        siblingDiscountApplied: j['siblingDiscountApplied'],
        status: j['status'],
      );
}

class EnrolmentDetail {
  final int id;
  final int childId;
  final String childName;
  final String status;
  final List<String> preferredDays;
  final String? preferredTimeFrom;
  final String? preferredTimeTo;
  final String? parentNotes;
  final ClassSummary? assignedClass;
  final List<FeeRecord> fees;
  final String? latestDecisionNote;
  final bool canEdit;
  final bool canCancel;

  EnrolmentDetail({
    required this.id,
    required this.childId,
    required this.childName,
    required this.status,
    required this.preferredDays,
    this.preferredTimeFrom,
    this.preferredTimeTo,
    this.parentNotes,
    this.assignedClass,
    required this.fees,
    this.latestDecisionNote,
    required this.canEdit,
    required this.canCancel,
  });

  factory EnrolmentDetail.fromJson(Map<String, dynamic> j) => EnrolmentDetail(
        id: j['id'],
        childId: j['childId'],
        childName: j['childName'],
        status: j['status'],
        preferredDays: List<String>.from(j['preferredDays']),
        preferredTimeFrom: (j['preferredTimeFrom'] as String?)?.substring(0, 5),
        preferredTimeTo: (j['preferredTimeTo'] as String?)?.substring(0, 5),
        parentNotes: j['parentNotes'],
        assignedClass: j['assignedClass'] == null ? null : ClassSummary.fromJson(j['assignedClass']),
        fees: (j['fees'] as List).map((f) => FeeRecord.fromJson(f)).toList(),
        latestDecisionNote: j['latestDecisionNote'],
        canEdit: j['canEdit'],
        canCancel: j['canCancel'],
      );
}

class StatusHistoryEntry {
  final String? fromStatus;
  final String toStatus;
  final String changedBy;
  final String? note;
  final DateTime changedAt;

  StatusHistoryEntry({this.fromStatus, required this.toStatus, required this.changedBy, this.note, required this.changedAt});

  factory StatusHistoryEntry.fromJson(Map<String, dynamic> j) => StatusHistoryEntry(
        fromStatus: j['fromStatus'],
        toStatus: j['toStatus'],
        changedBy: j['changedBy'],
        note: j['note'],
        changedAt: DateTime.parse(j['changedAt']),
      );
}

/// "PendingAdminApproval" -> "Pending admin approval"
String humanize(String value) {
  final spaced = value.replaceAllMapped(RegExp(r'([a-z])([A-Z])'), (m) => '${m[1]} ${m[2]!.toLowerCase()}');
  return spaced.isEmpty ? spaced : spaced[0].toUpperCase() + spaced.substring(1);
}
