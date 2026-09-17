import 'package:drift/drift.dart';

/// This device's own identity, decoded from its JWT at login and persisted
/// here — the durable local record of "which device am I," since nothing
/// else on-device keeps it (the token itself is refreshed/cleared on logout,
/// and the server only learns a device's id at pairing time). Single row in
/// practice, one device per app install.
///
/// Also caches the receipt number the server most recently issued to this
/// device, kept aligned after every completed payment. Recording sequential,
/// gap-auditable receipt numbers per device is a BIR requirement — see
/// backend ReceiptSequence — so an offline-capable checkout flow will need
/// to know the last number this device issued before it can safely predict
/// the next one. This table is that anchor point.
class DeviceIdentity extends Table {
  TextColumn get deviceId => text()();
  TextColumn get tenantId => text()();
  TextColumn get branchId => text()();
  IntColumn get lastKnownReceiptNumber => integer().nullable()();
  DateTimeColumn get updatedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {deviceId};
}
