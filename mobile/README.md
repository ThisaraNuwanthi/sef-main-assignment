# SKCA Enrol — Parent mobile app (Flutter)

Parents register, add children (with photos), request a class, and track each request's status,
timeline, approved class and fee. Talks **only** to the ASP.NET Core API.

| Concern | Choice |
|---|---|
| State management | **Provider** + `ChangeNotifier` (`lib/state/`) — see ADR 0002 |
| Navigation | go_router with a login `redirect` (protected screens) |
| Token storage | flutter_secure_storage (Android Keystore-backed encryption) |
| HTTP | `package:http` wrapped in `lib/api/api_client.dart` (injectable for tests) |
| Photos | image_picker (camera/gallery), resized on-device, multipart upload |

## Run

```bash
flutter pub get
# Android emulator -> API on your computer:
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5056
# Real phone on the same Wi-Fi -> use your computer's LAN IP, e.g. http://192.168.1.20:5056
```

## Test

```bash
flutter analyze
flutter test
```

## Release APK

```bash
flutter build apk --release --dart-define=API_BASE_URL=https://sef-main-assignment-production.up.railway.app
# -> build/app/outputs/flutter-apk/app-release.apk
```

Release builds only allow **https** (plain http is enabled in debug builds only, for the local API).
The release APK is signed with the debug key so it installs for the demo; a Play Store build would need
an upload keystore (kept out of git via `android/key.properties`).
