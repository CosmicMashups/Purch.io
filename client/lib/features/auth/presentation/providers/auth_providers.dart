import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/auth/jwt_claims.dart';
import '../../../../core/db/db_providers.dart';
import '../../../../core/errors/failure.dart';
import '../../../../core/network/api_client.dart';
import '../../../../core/session/session_scope.dart';
import '../../../../core/storage/secure_token_storage.dart';
import '../../data/auth_repository_impl.dart';
import '../../domain/auth_models.dart';
import '../../domain/auth_repository.dart';

part 'auth_providers.g.dart';

@Riverpod(keepAlive: true)
SecureTokenStorage secureTokenStorage(Ref ref) {
  return SecureTokenStorage();
}

@Riverpod(keepAlive: true)
ApiClient apiClient(Ref ref) {
  return ApiClient(
    tokenStorage: ref.watch(secureTokenStorageProvider),
    // The refresh token itself expired/was revoked — ApiClient already
    // cleared storage; this just makes the router notice and fall back to
    // the unlock or login screen instead of the app silently 401ing forever.
    onSessionExpired: resetSessionScope,
  );
}

@Riverpod(keepAlive: true)
AuthRepository authRepository(Ref ref) {
  return AuthRepositoryImpl(
    apiClient: ref.watch(apiClientProvider),
    tokenStorage: ref.watch(secureTokenStorageProvider),
    deviceIdentityDao: ref.watch(deviceIdentityDaoProvider),
  );
}

/// Whether a session already exists on this device at app startup — read once
/// on launch to decide which screen to land on (login vs. the app shell).
@riverpod
Future<bool> hasStoredSession(Ref ref) {
  return ref.watch(authRepositoryProvider).hasStoredSession();
}

/// Whether this device has been paired (holds a device credential).
@riverpod
Future<bool> hasDeviceCredential(Ref ref) {
  return ref.watch(authRepositoryProvider).hasDeviceCredential();
}

/// The stored token's "role" claim, read directly off the token rather than
/// a second stored flag — a Kiosk-role token routes the app to the kiosk
/// shell at startup instead of the staff app shell (see PurchApp).
@riverpod
Future<String?> storedSessionRole(Ref ref) async {
  final token = await ref.watch(secureTokenStorageProvider).readAccessToken();
  return token == null ? null : roleClaimFromJwt(token);
}

/// What the gate learns when it opens a session for an already-paired device
/// whose access token is gone (after a restart, or once the refresh token
/// lapsed): either the device now holds fresh tokens, it needs a person to
/// unlock it, or the server could not be asked right now.
enum PairedDeviceStartup { signedIn, needsUnlock, unreachable }

@riverpod
Future<PairedDeviceStartup> pairedDeviceStartup(Ref ref) async {
  final repository = ref.watch(authRepositoryProvider);
  try {
    final session = await repository.startDeviceSession();
    return session.requiresStaff
        ? PairedDeviceStartup.needsUnlock
        : PairedDeviceStartup.signedIn;
  } on UnauthorizedFailure {
    // The credential was removed by the repository: the device must pair again.
    return PairedDeviceStartup.unreachable;
  } on Failure {
    return PairedDeviceStartup.unreachable;
  }
}

/// Drives the email and password sign-in: call `signIn(...)`, watch the
/// AsyncValue for loading/error state. The data is non-null only when the
/// person must pick a business before signing in again.
@riverpod
class SignInController extends _$SignInController {
  @override
  FutureOr<List<BusinessChoice>?> build() => null;

  Future<bool> signIn({
    required String email,
    required String password,
    String? tenantId,
  }) async {
    state = const AsyncLoading();
    final repository = ref.read(authRepositoryProvider);

    state = await AsyncValue.guard(() async {
      final outcome = await repository.signIn(
        email: email,
        password: password,
        tenantId: tenantId,
      );
      return switch (outcome) {
        SignedIn() => null,
        ChooseBusiness(:final businesses) => businesses,
      };
    });
    return !state.hasError && state.valueOrNull == null;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Drives pairing a device with its one-time code.
@riverpod
class PairController extends _$PairController {
  @override
  FutureOr<DeviceSession?> build() => null;

  Future<DeviceSession?> pair(String pairingCode) async {
    state = const AsyncLoading();
    final repository = ref.read(authRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.pairDevice(pairingCode: pairingCode),
    );
    return state.valueOrNull;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// The people who may unlock this device, for the lock screen.
@riverpod
Future<DeviceRoster> deviceRoster(Ref ref) {
  return ref.watch(authRepositoryProvider).roster();
}

/// Drives a PIN unlock for one person.
@riverpod
class UnlockController extends _$UnlockController {
  @override
  FutureOr<void> build() {
    // No-op initial state: locked, no error, not loading.
  }

  Future<bool> unlock({required String membershipId, required String pin}) async {
    state = const AsyncLoading();
    final repository = ref.read(authRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.unlock(membershipId: membershipId, pin: pin),
    );
    return !state.hasError;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
