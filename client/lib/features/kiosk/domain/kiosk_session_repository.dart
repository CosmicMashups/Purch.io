/// Pairs a kiosk terminal by its device pairing code plus the device's own
/// pairing PIN (set by an admin in Manage Devices, not a staff PIN — a kiosk
/// is customer-facing and no one is "logging into" it). The PIN stops a
/// leaked/guessed pairing code alone from being enough to pair a rogue kiosk
/// as this tenant. On success the access token is persisted as a side
/// effect, same contract as AuthRepository.login.
abstract class KioskSessionRepository {
  Future<void> pair({required String devicePairingCode, required String pairingPin});
}
