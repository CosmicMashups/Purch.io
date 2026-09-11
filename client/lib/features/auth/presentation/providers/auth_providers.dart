import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/auth/jwt_claims.dart';
import '../../../../core/errors/failure.dart';
import '../../../../core/network/api_client.dart';
import '../../../../core/storage/secure_token_storage.dart';
import '../../data/auth_repository_impl.dart';
import '../../domain/auth_repository.dart';

part 'auth_providers.g.dart';

@Riverpod(keepAlive: true)
SecureTokenStorage secureTokenStorage(Ref ref) {
  return SecureTokenStorage();
}

@Riverpod(keepAlive: true)
ApiClient apiClient(Ref ref) {
  return ApiClient(tokenStorage: ref.watch(secureTokenStorageProvider));
}

@Riverpod(keepAlive: true)
AuthRepository authRepository(Ref ref) {
  return AuthRepositoryImpl(
    apiClient: ref.watch(apiClientProvider),
    tokenStorage: ref.watch(secureTokenStorageProvider),
  );
}

/// Whether a session already exists on this device at app startup — read once
/// on launch to decide which screen to land on (login vs. the app shell).
@riverpod
Future<bool> hasStoredSession(Ref ref) {
  return ref.watch(authRepositoryProvider).hasStoredSession();
}

/// The stored token's "role" claim, read directly off the token rather than
/// a second stored flag — a Kiosk-role token routes the app to the kiosk
/// shell at startup instead of the staff app shell (see PurchApp).
@riverpod
Future<String?> storedSessionRole(Ref ref) async {
  final token = await ref.watch(secureTokenStorageProvider).readAccessToken();
  return token == null ? null : roleClaimFromJwt(token);
}

/// Drives the login screen: call `login(...)`, watch this provider's
/// AsyncValue for loading/error/data state. Kept separate from
/// `authRepository` so the screen only depends on what it actually needs —
/// the in-flight state of *this* login attempt, not the repository itself.
@riverpod
class LoginController extends _$LoginController {
  @override
  FutureOr<void> build() {
    // No-op initial state: not logged in, no error, not loading.
  }

  Future<void> login({
    required String devicePairingCode,
    required String pin,
  }) async {
    state = const AsyncLoading();
    final repository = ref.read(authRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.login(devicePairingCode: devicePairingCode, pin: pin),
    );
  }

  /// The typed Failure behind the current error state, if any — screens use
  /// this instead of re-parsing `state.error`, which Riverpod only exposes
  /// as `Object?`.
  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
