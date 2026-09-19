import 'dart:async';
import 'dart:convert';

import '../domain/offline_limits.dart';
import '../domain/transaction_models.dart';

enum SaleQueueStatus { pending, syncing, synced, rejected, dismissed }

/// One sale completed at the counter while offline, waiting to be recorded on
/// the server.
class SaleQueueEntry {
  const SaleQueueEntry({
    required this.saleId,
    required this.receiptNumber,
    required this.totalAmount,
    required this.soldAt,
    required this.request,
    this.status = SaleQueueStatus.pending,
    this.attempts = 0,
    this.lastError,
  });

  final String saleId;
  final int receiptNumber;
  final double totalAmount;
  final DateTime soldAt;

  /// The checkout request to send, exactly as it will go on the wire.
  final CheckoutRequest request;
  final SaleQueueStatus status;
  final int attempts;
  final String? lastError;

  String get requestJson => jsonEncode(request.toJson());
}

/// Durable storage for [SaleQueueEntry]s, scoped to the signed-in tenant and
/// this terminal. Kept behind an interface so the repository and the sync
/// worker are testable without a database.
abstract class SaleQueueStore {
  Future<void> enqueue(SaleQueueEntry entry);

  /// Unsent sales, oldest first.
  Future<List<SaleQueueEntry>> pending();

  Future<List<SaleQueueEntry>> reviewable();

  Stream<List<SaleQueueEntry>> watchReviewable();

  Future<void> markSyncing(String saleId);

  /// Back to pending after a failed attempt that says nothing about the sale
  /// itself (no connection, server down) — counted, but never gives up.
  Future<void> markPending(String saleId, {String? error});

  Future<void> markSynced(String saleId);

  /// The server refused the sale for good (e.g. an item was deleted). The
  /// customer already has a receipt, so this must be seen by a person.
  Future<void> markRejected(String saleId, String reason);

  Future<void> dismiss(String saleId);

  /// Rows left 'syncing' by a crash go back to pending.
  Future<void> resetStuck();

  Future<void> purgeOldSynced(Duration keepFor);
}

/// Summary of the queue for limits and the on-screen banner.
class SaleQueueStats {
  const SaleQueueStats({
    this.unsynced = 0,
    this.rejected = 0,
    this.oldestUnsyncedAt,
  });

  factory SaleQueueStats.of(List<SaleQueueEntry> entries) {
    final unsynced =
        entries
            .where(
              (e) =>
                  e.status == SaleQueueStatus.pending ||
                  e.status == SaleQueueStatus.syncing,
            )
            .toList();
    DateTime? oldest;
    for (final entry in unsynced) {
      if (oldest == null || entry.soldAt.isBefore(oldest)) {
        oldest = entry.soldAt;
      }
    }
    return SaleQueueStats(
      unsynced: unsynced.length,
      rejected: entries.where((e) => e.status == SaleQueueStatus.rejected).length,
      oldestUnsyncedAt: oldest,
    );
  }

  final int unsynced;
  final int rejected;
  final DateTime? oldestUnsyncedAt;

  bool get isEmpty => unsynced == 0 && rejected == 0;

  OfflineLevel level(OfflineLimits limits, DateTime now) =>
      limits.evaluate(
        unsyncedCount: unsynced,
        oldestUnsyncedAt: oldestUnsyncedAt,
        now: now,
      );
}

/// In-memory [SaleQueueStore] — tests, and a safe default.
class MemorySaleQueueStore implements SaleQueueStore {
  final entries = <String, SaleQueueEntry>{};
  final _changes = StreamController<void>.broadcast();

  SaleQueueEntry? operator [](String saleId) => entries[saleId];

  void _set(String id, SaleQueueEntry Function(SaleQueueEntry) change) {
    final current = entries[id];
    if (current != null) {
      entries[id] = change(current);
      _changes.add(null);
    }
  }

  SaleQueueEntry _copy(
    SaleQueueEntry e, {
    SaleQueueStatus? status,
    int? attempts,
    String? lastError,
  }) => SaleQueueEntry(
    saleId: e.saleId,
    receiptNumber: e.receiptNumber,
    totalAmount: e.totalAmount,
    soldAt: e.soldAt,
    request: e.request,
    status: status ?? e.status,
    attempts: attempts ?? e.attempts,
    lastError: lastError ?? e.lastError,
  );

  @override
  Future<void> enqueue(SaleQueueEntry entry) async {
    entries[entry.saleId] = entry;
    _changes.add(null);
  }

  List<SaleQueueEntry> _where(bool Function(SaleQueueEntry) test) =>
      entries.values.where(test).toList()
        ..sort((a, b) => a.receiptNumber.compareTo(b.receiptNumber));

  @override
  Future<List<SaleQueueEntry>> pending() async =>
      _where((e) => e.status == SaleQueueStatus.pending);

  bool _reviewable(SaleQueueEntry e) =>
      e.status == SaleQueueStatus.pending ||
      e.status == SaleQueueStatus.syncing ||
      e.status == SaleQueueStatus.rejected;

  @override
  Future<List<SaleQueueEntry>> reviewable() async => _where(_reviewable);

  @override
  Stream<List<SaleQueueEntry>> watchReviewable() async* {
    yield _where(_reviewable);
    await for (final _ in _changes.stream) {
      yield _where(_reviewable);
    }
  }

  @override
  Future<void> markSyncing(String saleId) async =>
      _set(saleId, (e) => _copy(e, status: SaleQueueStatus.syncing));

  @override
  Future<void> markPending(String saleId, {String? error}) async => _set(
    saleId,
    (e) => _copy(
      e,
      status: SaleQueueStatus.pending,
      attempts: e.attempts + 1,
      lastError: error,
    ),
  );

  @override
  Future<void> markSynced(String saleId) async =>
      _set(saleId, (e) => _copy(e, status: SaleQueueStatus.synced));

  @override
  Future<void> markRejected(String saleId, String reason) async => _set(
    saleId,
    (e) => _copy(e, status: SaleQueueStatus.rejected, lastError: reason),
  );

  @override
  Future<void> dismiss(String saleId) async =>
      _set(saleId, (e) => _copy(e, status: SaleQueueStatus.dismissed));

  @override
  Future<void> resetStuck() async {
    for (final e in entries.values.toList()) {
      if (e.status == SaleQueueStatus.syncing) {
        entries[e.saleId] = _copy(e, status: SaleQueueStatus.pending);
      }
    }
  }

  @override
  Future<void> purgeOldSynced(Duration keepFor) async {}
}
