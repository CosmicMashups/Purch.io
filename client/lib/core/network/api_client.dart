import 'package:dio/dio.dart';

import '../config/app_config.dart';
import '../storage/secure_token_storage.dart';

/// Endpoints that issue tokens themselves — a 401 from one of these is a
/// genuine "wrong credentials"/"bad refresh token" response, not an expired
/// access token, so it must never trigger the refresh-and-retry dance below
/// (that would either loop forever or mask the real error).
const _tokenIssuingPaths = ['/auth/login', '/auth/admin-login', '/auth/refresh', '/kiosk/session'];

/// The single configured Dio instance the whole app talks to the backend
/// through. Every feature's repository takes this in, rather than each one
/// creating its own HTTP client — so the base URL, auth header, and timeouts
/// are configured in exactly one place.
///
/// Also owns silent access-token refresh: the backend issues short-lived
/// access tokens (see JwtTokenService) precisely because this interceptor
/// exists to renew them without the user ever seeing a 401 or having to log
/// in again mid-shift.
class ApiClient {
  ApiClient({
    required SecureTokenStorage tokenStorage,
    Dio? dio,
    Dio? refreshDio,
    this.onSessionExpired,
  }) : _tokenStorage = tokenStorage,
       dio =
           dio ??
           Dio(
             BaseOptions(
               baseUrl: AppConfig.apiBaseUrl,
               connectTimeout: const Duration(seconds: 10),
               receiveTimeout: const Duration(seconds: 15),
             ),
           ),
       // A separate Dio with none of this client's interceptors — the
       // refresh call must never itself go through the 401-retry logic
       // below, or a rejected refresh token would recurse forever.
       _refreshDio =
           refreshDio ??
           Dio(
             BaseOptions(
               baseUrl: AppConfig.apiBaseUrl,
               connectTimeout: const Duration(seconds: 10),
               receiveTimeout: const Duration(seconds: 15),
             ),
           ) {
    this.dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          final token = await _tokenStorage.readAccessToken();
          if (token != null) {
            options.headers['Authorization'] = 'Bearer $token';
          }
          handler.next(options);
        },
        onError: (error, handler) async {
          final path = error.requestOptions.path;
          final alreadyRetried = error.requestOptions.extra['retried'] == true;

          if (error.response?.statusCode != 401 ||
              _tokenIssuingPaths.contains(path) ||
              alreadyRetried) {
            handler.next(error);
            return;
          }

          String? newAccessToken;
          try {
            newAccessToken = await _refreshAccessToken();
          } on DioException catch (refreshError) {
            // Only a genuine 401 from /auth/refresh means the refresh token
            // itself is invalid/expired/revoked — that's the one case where
            // signing the user out is correct. Anything else (a dropped wifi
            // connection, a request timeout, a transient 5xx) is a network
            // hiccup, not a dead session: clearing tokens here would force a
            // full re-login over what's often a few seconds of bad signal,
            // which is exactly what was happening periodically during a
            // shift. Leave the stored tokens alone so the next request gets
            // another chance to refresh.
            if (refreshError.response?.statusCode == 401) {
              await _tokenStorage.clear();
              onSessionExpired?.call();
            }
            handler.next(error);
            return;
          }

          if (newAccessToken == null) {
            // No refresh token was even stored — genuinely not authenticated.
            await _tokenStorage.clear();
            onSessionExpired?.call();
            handler.next(error);
            return;
          }

          try {
            final retryOptions = error.requestOptions;
            retryOptions.extra['retried'] = true;
            retryOptions.headers['Authorization'] = 'Bearer $newAccessToken';
            final response = await this.dio.fetch<dynamic>(retryOptions);
            handler.resolve(response);
          } on DioException catch (retryError) {
            handler.next(retryError);
          }
        },
      ),
    );
  }

  final Dio dio;
  final Dio _refreshDio;
  final SecureTokenStorage _tokenStorage;

  /// Called once a stored refresh token turns out to be invalid/expired too
  /// — the app is no longer authenticated and should fall back to the login
  /// screen (see auth_providers.dart, which wires this to invalidate the
  /// auth-gate providers).
  final void Function()? onSessionExpired;

  /// Concurrent requests that 401 around the same time must share one
  /// refresh call rather than each redeeming (and rotating away) the same
  /// single-use refresh token — only the first would succeed.
  Future<String?>? _refreshInFlight;

  Future<String?> _refreshAccessToken() {
    return _refreshInFlight ??= _doRefresh().whenComplete(() {
      _refreshInFlight = null;
    });
  }

  /// Throws DioException on failure (rather than swallowing it to null) so
  /// the caller can tell a genuine 401 (invalid/expired/revoked refresh
  /// token — sign the user out) apart from a transient network/server
  /// failure (leave the stored tokens alone and let the next request retry).
  Future<String?> _doRefresh() async {
    final refreshToken = await _tokenStorage.readRefreshToken();
    if (refreshToken == null) {
      return null;
    }

    final response = await _refreshDio.post<Map<String, dynamic>>(
      '/auth/refresh',
      data: {'refreshToken': refreshToken},
    );

    final newAccessToken = response.data?['accessToken'] as String?;
    final newRefreshToken = response.data?['refreshToken'] as String?;
    if (newAccessToken == null || newRefreshToken == null) {
      return null;
    }

    await _tokenStorage.saveTokens(
      accessToken: newAccessToken,
      refreshToken: newRefreshToken,
    );
    return newAccessToken;
  }
}
