import 'package:purch_client/core/storage/secure_token_storage.dart';

/// An in-memory token store for widget tests, so nothing reaches the platform keychain (which never answers
/// in a test and would leave a screen waiting on it forever).
class FakeTokenStorage extends SecureTokenStorage {
  FakeTokenStorage({this.accessToken, this.refreshToken});

  String? accessToken;
  String? refreshToken;
  String? supervisorAttestation;

  @override
  Future<String?> readAccessToken() async => accessToken;

  @override
  Future<String?> readRefreshToken() async => refreshToken;

  @override
  Future<void> saveAccessToken(String token) async => accessToken = token;

  @override
  Future<void> saveRefreshToken(String token) async => refreshToken = token;

  @override
  Future<String?> readSupervisorAttestation() async => supervisorAttestation;

  @override
  Future<void> saveSupervisorAttestation(String attestation) async =>
      supervisorAttestation = attestation;

  @override
  Future<void> clearSupervisorAttestation() async => supervisorAttestation = null;
}
