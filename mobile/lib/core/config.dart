/// Where the API lives.
///
/// The default is the Android emulator's alias for the machine running it, which is what
/// `flutter run` on an emulator needs. On a real phone the API is on the office network, so pass
/// the address in at build time:
///
///   flutter run --dart-define=API_BASE_URL=http://192.168.1.5:5207
class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5207',
  );

  /// How long to wait before giving up on a request. Short, because a salesperson standing in a
  /// shop should find out quickly that there is no signal and carry on working offline.
  static const Duration requestTimeout = Duration(seconds: 15);

  /// How often to try the outbox again while the app is open and online.
  static const Duration syncInterval = Duration(minutes: 3);
}
