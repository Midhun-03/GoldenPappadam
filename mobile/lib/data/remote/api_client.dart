import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../core/config.dart';

/// Something the server said no to, in words worth showing a salesperson.
class ApiException implements Exception {
  ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  /// A refusal: the server understood and declined. Retrying unchanged will not help.
  bool get isRefusal => statusCode != null && statusCode! >= 400 && statusCode! < 500;

  @override
  String toString() => message;
}

/// No signal, or the server is unwell. The work is kept and tried again.
class OfflineException implements Exception {
  OfflineException([this.message = 'No connection.']);

  final String message;

  @override
  String toString() => message;
}

/// Where the tokens live. The Android keystore, never shared preferences, and never in the
/// database next to the business data.
class TokenStore {
  TokenStore([FlutterSecureStorage? storage])
      : _storage = storage ?? const FlutterSecureStorage();

  static const _accessKey = 'access_token';
  static const _refreshKey = 'refresh_token';

  final FlutterSecureStorage _storage;

  Future<String?> readAccess() => _storage.read(key: _accessKey);

  Future<String?> readRefresh() => _storage.read(key: _refreshKey);

  Future<void> save(String access, String refresh) async {
    await _storage.write(key: _accessKey, value: access);
    await _storage.write(key: _refreshKey, value: refresh);
  }

  Future<void> clear() async {
    await _storage.delete(key: _accessKey);
    await _storage.delete(key: _refreshKey);
  }
}

/// The only thing in the app that talks to the network.
///
/// It puts the bearer token on every request and, when one comes back 401, refreshes once and
/// replays it. A salesperson opening the app after a week should not have to think about tokens.
class ApiClient {
  ApiClient({Dio? dio, TokenStore? tokens, this.onSignedOut})
      : _tokens = tokens ?? TokenStore(),
        _dio = dio ??
            Dio(BaseOptions(
              baseUrl: AppConfig.apiBaseUrl,
              connectTimeout: AppConfig.requestTimeout,
              receiveTimeout: AppConfig.requestTimeout,
              sendTimeout: AppConfig.requestTimeout,
              contentType: 'application/json',
            )) {
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        if (options.extra['anonymous'] != true) {
          final token = await _tokens.readAccess();
          if (token != null) options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
      onError: (error, handler) async {
        final shouldRefresh = error.response?.statusCode == 401 &&
            error.requestOptions.extra['retried'] != true &&
            error.requestOptions.extra['anonymous'] != true;

        if (!shouldRefresh) return handler.next(error);

        if (!await _refresh()) {
          // The refresh token is gone or the account was deactivated. Nothing to do but sign in
          // again - and the outbox survives that, because signing out never discards work.
          onSignedOut?.call();
          return handler.next(error);
        }

        try {
          error.requestOptions.extra['retried'] = true;
          handler.resolve(await _dio.fetch(error.requestOptions));
        } on DioException catch (retryError) {
          handler.next(retryError);
        }
      },
    ));
  }

  final Dio _dio;
  final TokenStore _tokens;

  /// Called when the session cannot be renewed and the salesperson has to sign in again.
  final void Function()? onSignedOut;

  TokenStore get tokens => _tokens;

  Future<bool> get hasSession async => await _tokens.readAccess() != null;

  Future<Map<String, dynamic>> get(String path, {Map<String, dynamic>? query}) async =>
      _send(() => _dio.get<Map<String, dynamic>>(path, queryParameters: query));

  Future<Map<String, dynamic>> post(String path, Object? body, {bool anonymous = false}) async =>
      _send(() => _dio.post<Map<String, dynamic>>(
            path,
            data: body,
            options: Options(extra: {'anonymous': anonymous}),
          ));

  Future<void> signIn(String email, String password) async {
    final body = await post(
      '/api/auth/mobile/login',
      {'email': email, 'password': password},
      anonymous: true,
    );

    await _tokens.save(body['accessToken'] as String, body['refreshToken'] as String);
  }

  Future<void> signOut() => _tokens.clear();

  Future<bool> _refresh() async {
    final refreshToken = await _tokens.readRefresh();
    if (refreshToken == null) return false;

    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/auth/mobile/refresh',
        data: {'refreshToken': refreshToken},
        options: Options(extra: {'anonymous': true}),
      );

      final body = response.data!;
      await _tokens.save(body['accessToken'] as String, body['refreshToken'] as String);

      return true;
    } on DioException {
      return false;
    }
  }

  /// Turns Dio's failures into two kinds the app actually treats differently: the server refused
  /// this (keep it, show the salesperson) and there is no server right now (keep it, try later).
  Future<Map<String, dynamic>> _send(
      Future<Response<Map<String, dynamic>>> Function() request) async {
    try {
      final response = await request();

      return response.data ?? <String, dynamic>{};
    } on DioException catch (error) {
      final status = error.response?.statusCode;

      if (status == null || status >= 500) {
        throw OfflineException(_describeConnection(error));
      }

      throw ApiException(_describeProblem(error.response?.data) ?? 'Something went wrong.',
          statusCode: status);
    }
  }

  static String _describeConnection(DioException error) => switch (error.type) {
        DioExceptionType.connectionTimeout ||
        DioExceptionType.sendTimeout ||
        DioExceptionType.receiveTimeout =>
          'The network is too slow to reach the office.',
        DioExceptionType.connectionError => 'No connection.',
        _ => 'The office system is not answering. It will try again.',
      };

  /// The API answers with problem details, the same shape the admin panel reads.
  static String? _describeProblem(Object? data) {
    if (data is! Map) return null;

    final detail = data['detail'];
    if (detail is String && detail.isNotEmpty) return detail;

    final errors = data['errors'];
    if (errors is Map) {
      final messages = errors.values.whereType<List>().expand((list) => list).join(' ');
      if (messages.isNotEmpty) return messages;
    }

    final title = data['title'];

    return title is String ? title : null;
  }
}
