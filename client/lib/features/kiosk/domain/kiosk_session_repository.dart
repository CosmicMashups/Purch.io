/// Pairs a kiosk terminal by its device pairing code alone — no staff PIN,
/// since a kiosk is customer-facing and no one is "logging into" it. On
/// success the access token is persisted as a side effect, same contract as
/// AuthRepository.login.
abstract class KioskSessionRepository {
  Future<void> pair({required String devicePairingCode});
}
