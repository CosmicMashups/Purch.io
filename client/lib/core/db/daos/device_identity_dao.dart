import 'package:drift/drift.dart';

import '../app_database.dart';
import '../tables/device_identity_table.dart';

part 'device_identity_dao.g.dart';

@DriftAccessor(tables: [DeviceIdentity])
class DeviceIdentityDao extends DatabaseAccessor<AppDatabase>
    with _$DeviceIdentityDaoMixin {
  DeviceIdentityDao(super.db);

  /// Upserts this device's identity, decoded fresh from every login's JWT.
  /// A device is re-paired rarely enough that overwriting beats reconciling,
  /// and re-pairing onto different tenant/branch is exactly the case where
  /// the old row must not linger.
  Future<void> saveIdentity({
    required String deviceId,
    required String tenantId,
    required String branchId,
  }) async {
    // Logging in again on the same device and tenant must not forget which
    // receipt number this device last issued — it numbers its next sale from
    // it. Only a different device or tenant starts a fresh sequence.
    final existing = await getIdentity();
    final sameTerminal =
        existing != null &&
        existing.deviceId == deviceId &&
        existing.tenantId == tenantId;
    final keptReceiptNumber =
        sameTerminal ? existing.lastKnownReceiptNumber : null;

    await delete(deviceIdentity).go();
    await into(deviceIdentity).insert(
      DeviceIdentityCompanion.insert(
        deviceId: deviceId,
        tenantId: tenantId,
        branchId: branchId,
        lastKnownReceiptNumber: Value(keptReceiptNumber),
        updatedAt: DateTime.now(),
      ),
    );
  }

  Future<DeviceIdentityData?> getIdentity() {
    return select(deviceIdentity).getSingleOrNull();
  }

  /// Keeps the locally cached sequence aligned with the number the server
  /// just issued for a completed payment, so it's never stale by more than
  /// one sale — the anchor an offline-capable checkout would resume from.
  Future<void> recordIssuedReceiptNumber(int receiptNumber) async {
    final identity = await getIdentity();
    if (identity == null) {
      return;
    }
    // Never move backwards: a late-arriving lower number must not rewind the
    // counter this device numbers its next sale from.
    if ((identity.lastKnownReceiptNumber ?? 0) >= receiptNumber) {
      return;
    }
    await (update(
      deviceIdentity,
    )..where((row) => row.deviceId.equals(identity.deviceId))).write(
      DeviceIdentityCompanion(
        lastKnownReceiptNumber: Value(receiptNumber),
        updatedAt: Value(DateTime.now()),
      ),
    );
  }
}
