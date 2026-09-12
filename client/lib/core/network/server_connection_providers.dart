import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../features/auth/presentation/providers/auth_providers.dart';
import '../config/app_config.dart';
import '../errors/failure.dart';
import '../storage/server_connection_storage.dart';

part 'server_connection_providers.g.dart';

@Riverpod(keepAlive: true)
ServerConnectionStorage serverConnectionStorage(Ref ref) {
  return ServerConnectionStorage();
}

/// Drives ServerConnectionScreen: tests a candidate Local/on-prem server
/// address against its /health endpoint before committing to it, then
/// persists it and updates both the build-time-default override (AppConfig)
/// and the already-constructed, keepAlive ApiClient's live Dio instance — so
/// the change takes effect immediately, with no app restart required.
@riverpod
class ServerConnectionController extends _$ServerConnectionController {
  @override
  FutureOr<void> build() {}

  Future<bool> testAndSave(String rawBaseUrl) async {
    final baseUrl = _normalize(rawBaseUrl);

    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _testConnection(baseUrl));

    if (state.hasError) {
      return false;
    }

    await ref.read(serverConnectionStorageProvider).saveBaseUrl(baseUrl);
    AppConfig.setApiBaseUrlOverride(baseUrl);
    ref.read(apiClientProvider).dio.options.baseUrl = baseUrl;

    return true;
  }

  Future<void> clear() async {
    await ref.read(serverConnectionStorageProvider).clear();
    AppConfig.setApiBaseUrlOverride(null);
    ref.read(apiClientProvider).dio.options.baseUrl = AppConfig.apiBaseUrl;
  }

  Future<void> _testConnection(String baseUrl) async {
    final probe = Dio(
      BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: const Duration(seconds: 5),
        receiveTimeout: const Duration(seconds: 5),
      ),
    );

    try {
      final response = await probe.get<Object?>('/health');
      if (response.statusCode != 200) {
        throw const NetworkFailure(
          'The server responded, but not as expected — double-check the address.',
        );
      }
    } on DioException {
      throw const NetworkFailure(
        'Could not reach a Purch.io server at that address. Check the IP/port and that both devices are on the same network.',
      );
    }
  }

  String _normalize(String rawBaseUrl) {
    var url = rawBaseUrl.trim();
    if (!url.startsWith('http://') && !url.startsWith('https://')) {
      url = 'http://$url';
    }
    while (url.endsWith('/')) {
      url = url.substring(0, url.length - 1);
    }
    return url;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
