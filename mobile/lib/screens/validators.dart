// Form validators (same rules as the API's DTOs, for instant feedback).
// The server validates again; these are a convenience, not the security boundary.

String? validateEmail(String? value) {
  final v = value?.trim() ?? '';
  if (v.isEmpty) return 'Email is required.';
  if (!RegExp(r'^[^\s@]+@[^\s@]+\.[^\s@]+$').hasMatch(v)) return 'Enter a valid email address.';
  return null;
}

/// Matches RegisterRequest: 8+ characters with at least one letter and one number.
String? validateNewPassword(String? value) {
  final v = value ?? '';
  if (v.length < 8) return 'Use at least 8 characters.';
  if (!RegExp(r'[A-Za-z]').hasMatch(v) || !RegExp(r'\d').hasMatch(v)) return 'Use at least one letter and one number.';
  return null;
}

String? validateName(String? value) =>
    (value == null || value.trim().length < 2) ? 'Enter a name (at least 2 characters).' : null;

/// Matches SaveChildRequest: optional, 2-30 letters, digits, "_" or "-".
String? validateLichess(String? value) {
  final v = value?.trim() ?? '';
  if (v.isEmpty) return null;
  return RegExp(r'^[A-Za-z0-9_-]{2,30}$').hasMatch(v) ? null : 'Not a valid Lichess username.';
}

/// The academy teaches children aged 4 to 18.
String? validateDateOfBirth(DateTime? dob, {DateTime? today}) {
  if (dob == null) return 'Choose a date of birth.';
  final now = today ?? DateTime.now();
  var age = now.year - dob.year;
  if (DateTime(now.year, dob.month, dob.day).isAfter(now)) age--;
  if (age < 4 || age > 18) return 'Child must be between 4 and 18 years old.';
  return null;
}

/// Both times optional; if both are given, "to" must be after "from". Times are "HH:mm".
String? validateTimeRange(String? from, String? to) {
  if (from != null && to != null && to.compareTo(from) <= 0) return '"To" time must be after "From" time.';
  return null;
}
