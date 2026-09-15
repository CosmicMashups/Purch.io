import 'sales_dashboard_models.dart';

/// Bucket size for a sales-trend query. Home's segmented Day/Week/Month/Year
/// filter maps straight onto this; "Custom" picks the granularity that keeps
/// the chosen range readable (see [SalesTrendGranularity.forRange]).
enum SalesTrendGranularity { day, week, month, year }

extension SalesTrendGranularityLabel on SalesTrendGranularity {
  String get label => switch (this) {
    SalesTrendGranularity.day => 'Day',
    SalesTrendGranularity.week => 'Week',
    SalesTrendGranularity.month => 'Month',
    SalesTrendGranularity.year => 'Year',
  };

  /// The default lookback window each granularity shows when the user hasn't
  /// picked a custom range — enough buckets to read as a trend, not so many
  /// that the axis turns to mush.
  Duration get defaultLookback => switch (this) {
    SalesTrendGranularity.day => const Duration(days: 14),
    SalesTrendGranularity.week => const Duration(days: 7 * 12),
    SalesTrendGranularity.month => const Duration(days: 365),
    SalesTrendGranularity.year => const Duration(days: 365 * 5),
  };

  String get windowLabel => switch (this) {
    SalesTrendGranularity.day => 'Last 14 days, daily',
    SalesTrendGranularity.week => 'Last 12 weeks, weekly',
    SalesTrendGranularity.month => 'Last 12 months, monthly',
    SalesTrendGranularity.year => 'Last 5 years, yearly',
  };

  static SalesTrendGranularity forRange(DateTime from, DateTime to) {
    final days = to.difference(from).inDays.abs();
    if (days <= 31) return SalesTrendGranularity.day;
    if (days <= 182) return SalesTrendGranularity.week;
    if (days <= 365 * 3) return SalesTrendGranularity.month;
    return SalesTrendGranularity.year;
  }
}

/// One plotted bucket. [periodStart] is the bucket's first calendar day so
/// two series at the same granularity always line up.
class SalesTrendPoint {
  const SalesTrendPoint({
    required this.periodStart,
    required this.label,
    required this.revenue,
  });

  final DateTime periodStart;
  final String label;
  final double revenue;
}

/// A sales trend plus the comparable preceding window, so the caller can show
/// a real period-over-period delta rather than inventing one.
///
/// [previousTotal] is null when the underlying data doesn't reach far enough
/// back to cover a full prior window — Home renders "not enough history"
/// in that case instead of a fake-precise percentage.
class SalesTrendSeries {
  const SalesTrendSeries({
    required this.granularity,
    required this.from,
    required this.to,
    required this.points,
    required this.previousTotal,
    required this.coverageFrom,
    required this.coverageTo,
  });

  final SalesTrendGranularity granularity;
  final DateTime from;
  final DateTime to;
  final List<SalesTrendPoint> points;
  final double? previousTotal;

  /// The window the underlying data actually covered, which can be narrower
  /// than [from]..[to] — surfaced in the chart subtitle so nobody reads an
  /// empty tail as "no sales".
  final DateTime? coverageFrom;
  final DateTime? coverageTo;

  double get total => points.fold<double>(0, (sum, p) => sum + p.revenue);

  /// Period-over-period change as a fraction (0.12 == +12%). Null when there
  /// is no comparable prior window, or when the prior window was zero (a
  /// percentage change from zero is not a meaningful number).
  double? get changeFraction {
    final previous = previousTotal;
    if (previous == null || previous == 0) {
      return null;
    }
    return (total - previous) / previous;
  }

  bool get isEmpty => points.isEmpty || points.every((p) => p.revenue == 0);

  /// Buckets a flat list of daily revenue points into a trend series.
  ///
  /// This is the aggregation the backend does not (yet) expose: the
  /// `/reports/sales-dashboard` response carries a daily series, and every
  /// coarser granularity is a fold over it. Keeping it here as a pure
  /// function means the repository can be swapped for a real
  /// `/reports/sales-trend` endpoint later without touching the UI.
  static SalesTrendSeries fromDailyPoints(
    List<DailyRevenuePoint> daily, {
    required SalesTrendGranularity granularity,
    required DateTime from,
    required DateTime to,
  }) {
    final rangeStart = _dateOnly(from);
    final rangeEnd = _dateOnly(to);
    final windowDays = rangeEnd.difference(rangeStart).inDays + 1;
    final previousStart = rangeStart.subtract(Duration(days: windowDays));

    DateTime? coverageFrom;
    DateTime? coverageTo;
    final buckets = <DateTime, double>{};
    var previousTotal = 0.0;
    var sawPreviousWindowData = false;

    for (final point in daily) {
      final day = _dateOnly(point.date);
      if (coverageFrom == null || day.isBefore(coverageFrom)) {
        coverageFrom = day;
      }
      if (coverageTo == null || day.isAfter(coverageTo)) {
        coverageTo = day;
      }

      if (!day.isBefore(previousStart) && day.isBefore(rangeStart)) {
        previousTotal += point.revenue;
        sawPreviousWindowData = true;
        continue;
      }
      if (day.isBefore(rangeStart) || day.isAfter(rangeEnd)) {
        continue;
      }

      final key = _bucketStart(day, granularity);
      buckets[key] = (buckets[key] ?? 0) + point.revenue;
    }

    final keys = buckets.keys.toList()..sort();

    return SalesTrendSeries(
      granularity: granularity,
      from: rangeStart,
      to: rangeEnd,
      points: [
        for (final key in keys)
          SalesTrendPoint(
            periodStart: key,
            label: _bucketLabel(key, granularity),
            revenue: buckets[key]!,
          ),
      ],
      // Only claim a comparison when the data actually reached into the
      // prior window; otherwise the caller shows "not enough history".
      previousTotal:
          sawPreviousWindowData &&
                  coverageFrom != null &&
                  !coverageFrom.isAfter(previousStart)
              ? previousTotal
              : null,
      coverageFrom: coverageFrom,
      coverageTo: coverageTo,
    );
  }

  static DateTime _dateOnly(DateTime value) =>
      DateTime(value.year, value.month, value.day);

  static DateTime _bucketStart(
    DateTime day,
    SalesTrendGranularity granularity,
  ) => switch (granularity) {
    SalesTrendGranularity.day => day,
    // ISO weeks: Monday-start, so a "week" bucket matches how a store owner
    // reads their trading week.
    SalesTrendGranularity.week => day.subtract(
      Duration(days: day.weekday - DateTime.monday),
    ),
    SalesTrendGranularity.month => DateTime(day.year, day.month),
    SalesTrendGranularity.year => DateTime(day.year),
  };

  static const _monthNames = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec',
  ];

  static String _bucketLabel(
    DateTime start,
    SalesTrendGranularity granularity,
  ) => switch (granularity) {
    SalesTrendGranularity.day => '${_monthNames[start.month - 1]} ${start.day}',
    SalesTrendGranularity.week =>
      '${_monthNames[start.month - 1]} ${start.day}',
    SalesTrendGranularity.month =>
      '${_monthNames[start.month - 1]} ${start.year % 100}',
    SalesTrendGranularity.year => '${start.year}',
  };
}
