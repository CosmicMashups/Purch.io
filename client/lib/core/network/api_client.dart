import 'package:dio/dio.dart';

import '../config/app_config.dart';
import '../storage/secure_token_storage.dart';

/// The single configured Dio instance the whole app talks to the backend
/// through. Every feature's repository takes this in, rather than each one
/// creating its own HTTP client — so the base URL, auth header, and timeouts
/// are configured in exactly one place.
class ApiClient {
  ApiClient({required SecureTokenStorage tokenStorage, Dio? dio})
    : _tokenStorage = tokenStorage,
      dio =
          dio ??
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
      ),
    );
  }

  final Dio dio;
  final SecureTokenStorage _tokenStorage;
}
