import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/auth/domain/auth_repository.dart';

/// A hand-written fake (not a mocking-framework mock) so widget tests never
/// touch the real network layer. Configure `failureToThrow` to simulate a
/// rejected login, or leave it null to simulate success.
class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({this.failureToThrow});

  final Failure? failureToThrow;
  bool hasSession = false;
  bool loggedOutCalled = false;
  String? lastPairingCode;
  String? lastPin;

  @override
  Future<void> login({
    required String devicePairingCode,
    required String pin,
  }) async {
    lastPairingCode = devicePairingCode;
    lastPin = pin;
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    hasSession = true;
  }

  String? lastAdminEmail;
  String? lastAdminPassword;

  @override
  Future<void> loginAsAdmin({
    required String email,
    required String password,
  }) async {
    lastAdminEmail = email;
    lastAdminPassword = password;
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    hasSession = true;
  }

  @override
  Future<bool> hasStoredSession() async => hasSession;

  @override
  Future<void> logout() async {
    loggedOutCalled = true;
    hasSession = false;
  }
}
