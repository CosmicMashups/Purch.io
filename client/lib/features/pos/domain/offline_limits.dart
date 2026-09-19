/// How much offline selling a terminal may do before it must reconnect.
///
/// Every sale completed offline is money handed over on the strength of
/// prices, promos and stock the terminal last saw. The longer a terminal stays
/// disconnected, the more can be wrong (stale promos, a lost or stolen device
/// carrying unsynced sales), so selling offline is bounded by count and by age.
class OfflineLimits {
  const OfflineLimits({
    this.maxSales = 200,
    this.maxAge = const Duration(hours: 24),
    this.warnAt = 0.8,
  });

  /// The most sales that may wait unsynced.
  final int maxSales;

  /// The longest the oldest unsynced sale may wait.
  final Duration maxAge;

  /// Fraction of either limit at which the cashier starts being warned.
  final double warnAt;

  OfflineLevel evaluate({
    required int unsyncedCount,
    required DateTime? oldestUnsyncedAt,
    required DateTime now,
  }) {
    final countUsed = maxSales == 0 ? 1.0 : unsyncedCount / maxSales;
    final ageUsed =
        oldestUnsyncedAt == null
            ? 0.0
            : now.difference(oldestUnsyncedAt).inMilliseconds /
                maxAge.inMilliseconds;
    final used = countUsed > ageUsed ? countUsed : ageUsed;

    if (used >= 1) {
      return OfflineLevel.blocked;
    }
    return used >= warnAt ? OfflineLevel.warning : OfflineLevel.ok;
  }
}

enum OfflineLevel {
  /// Plenty of headroom (or nothing waiting).
  ok,

  /// Close to a limit — reconnect soon.
  warning,

  /// At a limit: no further sale may be completed offline until the queue syncs.
  blocked,
}
