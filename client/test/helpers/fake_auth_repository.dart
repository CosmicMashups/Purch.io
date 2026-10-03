import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/auth/domain/auth_models.dart';
import 'package:purch_client/features/auth/domain/auth_repository.dart';

/// A hand-written fake (not a mocking-framework mock) so widget tests never
/// touch the real network layer. Configure `failureToThrow` to simulate a
/// rejected sign-in, pairing or unlock, or leave it null to simulate success.
class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({
    this.failureToThrow,
    this.unlockFailure,
    this.businessesToChooseFrom,
    this.people = const [],
    this.deviceSession = const DeviceSession(
      requiresStaff: true,
      deviceType: PairedDeviceType.register,
      name: 'Front counter',
    ),
  });

  final Failure? failureToThrow;

  /// Thrown only by [unlock], so the roster still loads (a wrong PIN).
  final Failure? unlockFailure;

  /// When set, the first sign-in (without a tenant id) asks to choose one of these.
  final List<BusinessChoice>? businessesToChooseFrom;
  final List<RosterPerson> people;
  final DeviceSession deviceSession;

  bool hasSession = false;
  bool hasCredential = false;
  bool loggedOutCalled = false;
  bool unpairedCalled = false;

  String? lastEmail;
  String? lastPassword;
  String? lastTenantId;
  String? lastPairingCode;
  String? lastUnlockMembershipId;
  String? lastUnlockPin;

  @override
  Future<SignInOutcome> signIn({
    required String email,
    required String password,
    String? tenantId,
  }) async {
    lastEmail = email;
    lastPassword = password;
    lastTenantId = tenantId;
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    if (businessesToChooseFrom != null && tenantId == null) {
      return ChooseBusiness(businessesToChooseFrom!);
    }
    hasSession = true;
    return const SignedIn();
  }

  @override
  Future<DeviceSession> pairDevice({required String pairingCode}) async {
    lastPairingCode = pairingCode;
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    hasCredential = true;
    return deviceSession;
  }

  @override
  Future<bool> hasDeviceCredential() async => hasCredential;

  @override
  Future<DeviceSession> startDeviceSession() async {
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    return deviceSession;
  }

  @override
  Future<DeviceRoster> roster() async {
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    return DeviceRoster(deviceName: 'Front counter', people: people);
  }

  @override
  Future<void> unlock({
    required String membershipId,
    required String pin,
  }) async {
    lastUnlockMembershipId = membershipId;
    lastUnlockPin = pin;
    if (unlockFailure != null) {
      throw unlockFailure!;
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

  @override
  Future<void> unpair() async {
    unpairedCalled = true;
    hasCredential = false;
    hasSession = false;
  }
}
