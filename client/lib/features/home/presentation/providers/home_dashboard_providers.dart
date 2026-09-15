import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../reports/domain/sales_trend_models.dart';

part 'home_dashboard_providers.g.dart';

/// The window Home's charts are looking at. [isCustom] distinguishes "the
/// user picked these two dates on a calendar" from "this is the Week preset's
/// derived window", which matters for how the control labels itself.
class HomeTrendRange {
  const HomeTrendRange({
    required this.granularity,
    required this.from,
    required this.to,
    this.isCustom = false,
  });

  factory HomeTrendRange.preset(
    SalesTrendGranularity granularity, {
    DateTime? now,
  }) {
    final today = _dateOnly(now ?? DateTime.now());
    return HomeTrendRange(
      granularity: granularity,
      from: today.subtract(granularity.defaultLookback),
      to: today,
    );
  }

  factory HomeTrendRange.custom(DateTime from, DateTime to) {
    final start = _dateOnly(from);
    final end = _dateOnly(to);
    return HomeTrendRange(
      granularity: SalesTrendGranularityLabel.forRange(start, end),
      from: start,
      to: end,
      isCustom: true,
    );
  }

  final SalesTrendGranularity granularity;
  final DateTime from;
  final DateTime to;
  final bool isCustom;

  static DateTime _dateOnly(DateTime value) =>
      DateTime(value.year, value.month, value.day);

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

  static String formatDay(DateTime day) =>
      '${_monthNames[day.month - 1]} ${day.day}, ${day.year}';

  String get description =>
      isCustom
          ? '${formatDay(from)} — ${formatDay(to)}'
          : granularity.windowLabel;
}

/// One shared range across Home's charts: the sales trend and the movement
/// summary move together, so the page reads as one report rather than several
/// charts that each happen to be looking at a different fortnight.
@riverpod
class HomeTrendRangeController extends _$HomeTrendRangeController {
  @override
  HomeTrendRange build() =>
      HomeTrendRange.preset(SalesTrendGranularity.day);

  void selectPreset(SalesTrendGranularity granularity) {
    state = HomeTrendRange.preset(granularity);
  }

  void selectCustom(DateTime from, DateTime to) {
    state = HomeTrendRange.custom(from, to);
  }
}
