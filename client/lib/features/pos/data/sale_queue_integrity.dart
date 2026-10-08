import 'dart:convert';
import 'dart:math';

import 'package:crypto/crypto.dart';

import '../../../core/db/app_database.dart';
import '../../../core/db/daos/queued_sale_dao.dart';
import '../../../core/storage/secure_token_storage.dart';

/// Seals queued offline sales so that editing the database file cannot change what gets sent to the server.
///
/// A sale made offline is paid before the server hears of it, then sits in SQLite until the terminal reconnects. Anyone
/// with the file could lower a total, change a discount or swap the payment, and the app would replay the edited sale.
/// Each row therefore carries an HMAC-SHA256 over everything the server will be told, made with a random key kept in the
/// platform keystore (never in the database). A row that no longer matches its seal is refused, not sent.
///
/// This is a tamper check, not a substitute for the server: the server still prices every sale itself and judges every
/// discount. What it adds is that the phone or till's own storage cannot be used to alter a queued sale quietly.
class SaleQueueIntegrity {
  SaleQueueIntegrity({
    required Future<String?> Function() readKey,
    required Future<void> Function(String key) saveKey,
    required Future<bool> Function() readSealed,
    required Future<void> Function() markSealed,
  }) : _readKey = readKey,
       _saveKey = saveKey,
       _readSealed = readSealed,
       _markSealed = markSealed;

  factory SaleQueueIntegrity.fromStorage(SecureTokenStorage storage) =>
      SaleQueueIntegrity(
        readKey: storage.readQueueIntegrityKey,
        saveKey: storage.saveQueueIntegrityKey,
        readSealed: storage.readQueueSealed,
        markSealed: storage.markQueueSealed,
      );

  final Future<String?> Function() _readKey;
  final Future<void> Function(String key) _saveKey;
  final Future<bool> Function() _readSealed;
  final Future<void> Function() _markSealed;

  Future<List<int>>? _key;
  bool _legacyChecked = false;

  /// Loaded (or made) once per process, so parallel callers cannot make two different keys.
  Future<List<int>> _loadKey() => _key ??= _loadOrCreateKey();

  Future<List<int>> _loadOrCreateKey() async {
    final existing = await _readKey();
    if (existing != null && existing.isNotEmpty) {
      return base64Decode(existing);
    }
    final random = Random.secure();
    final bytes = List<int>.generate(32, (_) => random.nextInt(256));
    await _saveKey(base64Encode(bytes));
    return bytes;
  }

  /// The text that is sealed: every field the server is told about the sale, and nothing that legitimately changes
  /// afterwards (status, attempts, error). Time is whole seconds because that is what the database stores.
  static String canonical({
    required String id,
    required String tenantId,
    required String deviceId,
    required int receiptNumber,
    required double totalAmount,
    required String requestJson,
    required DateTime soldAt,
  }) => [
    id,
    tenantId,
    deviceId,
    receiptNumber.toString(),
    totalAmount.toString(),
    requestJson,
    (soldAt.toUtc().millisecondsSinceEpoch ~/ 1000).toString(),
  ].join('\u0001');

  Future<String> sign({
    required String id,
    required String tenantId,
    required String deviceId,
    required int receiptNumber,
    required double totalAmount,
    required String requestJson,
    required DateTime soldAt,
  }) async {
    final hmac = Hmac(sha256, await _loadKey());
    final text = canonical(
      id: id,
      tenantId: tenantId,
      deviceId: deviceId,
      receiptNumber: receiptNumber,
      totalAmount: totalAmount,
      requestJson: requestJson,
      soldAt: soldAt,
    );
    return base64Encode(hmac.convert(utf8.encode(text)).bytes);
  }

  /// True when the row still matches its seal. A row with no seal is not trusted (see [sealLegacyOnce]).
  Future<bool> verify(QueuedSale row) async {
    final seal = row.integrity;
    if (seal == null) {
      return false;
    }
    final expected = await sign(
      id: row.id,
      tenantId: row.tenantId,
      deviceId: row.deviceId,
      receiptNumber: row.receiptNumber,
      totalAmount: row.totalAmount,
      requestJson: row.requestJson,
      soldAt: row.soldAt,
    );
    return _constantTimeEquals(expected, seal);
  }

  /// Sales queued before sealing existed have no seal. They are sealed exactly once, the first time the queue is used
  /// after the upgrade. From then on a row without a seal is treated as tampered with: otherwise clearing the seal of an
  /// edited row would be a way round the check.
  Future<void> sealLegacyOnce(QueuedSaleDao dao) async {
    if (_legacyChecked) {
      return;
    }
    if (!await _readSealed()) {
      for (final row in await dao.listUnsealed()) {
        await dao.setIntegrity(
          row.id,
          await sign(
            id: row.id,
            tenantId: row.tenantId,
            deviceId: row.deviceId,
            receiptNumber: row.receiptNumber,
            totalAmount: row.totalAmount,
            requestJson: row.requestJson,
            soldAt: row.soldAt,
          ),
        );
      }
      await _markSealed();
    }
    _legacyChecked = true;
  }

  static bool _constantTimeEquals(String a, String b) {
    if (a.length != b.length) {
      return false;
    }
    var diff = 0;
    for (var i = 0; i < a.length; i++) {
      diff |= a.codeUnitAt(i) ^ b.codeUnitAt(i);
    }
    return diff == 0;
  }
}
