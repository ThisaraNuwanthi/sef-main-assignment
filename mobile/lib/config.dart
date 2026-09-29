/// App configuration passed at build time, e.g.
///   flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5056
///   flutter build apk --release --dart-define=API_BASE_URL=https://your-api.up.railway.app
///
/// 10.0.2.2 is how the Android emulator reaches "localhost" on your computer.
class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5056',
  );
}
