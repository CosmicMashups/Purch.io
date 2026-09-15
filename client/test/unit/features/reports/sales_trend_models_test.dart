import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/reports/domain/sales_dashboard_models.dart';
import 'package:purch_client/features/reports/domain/sales_trend_models.dart';

List<DailyRevenuePoint> _daily(DateTime start, int days, double perDay) => [
  for (var i = 0; i < days; i++)
    DailyRevenuePoint(date: start.add(Duration(days: i)), revenue: perDay),
];

void main() {
  group('SalesTrendSeries.fromDailyPoints', () {
    test('buckets daily points by day', () {
      final series = SalesTrendSeries.fromDailyPoints(
        _daily(DateTime(2026, 3, 1), 5, 100),
        granularity: SalesTrendGranularity.day,
        from: DateTime(2026, 3, 1),
        to: DateTime(2026, 3, 5),
      );

      expect(series.points, hasLength(5));
      expect(series.total, 500);
    });

    test('folds days into Monday-start weeks', () {
      // 2026-03-02 is a Monday; 14 days spans exactly two weeks.
      final series = SalesTrendSeries.fromDailyPoints(
        _daily(DateTime(2026, 3, 2), 14, 10),
        granularity: SalesTrendGranularity.week,
        from: DateTime(2026, 3, 2),
        to: DateTime(2026, 3, 15),
      );

      expect(series.points, hasLength(2));
      expect(series.points.every((p) => p.revenue == 70), isTrue);
      expect(series.points.first.periodStart, DateTime(2026, 3, 2));
    });

    test('folds days into calendar months', () {
      final series = SalesTrendSeries.fromDailyPoints(
        [
          ..._daily(DateTime(2026, 1, 1), 31, 1),
          ..._daily(DateTime(2026, 2, 1), 28, 2),
        ],
        granularity: SalesTrendGranularity.month,
        from: DateTime(2026, 1, 1),
        to: DateTime(2026, 2, 28),
      );

      expect(series.points, hasLength(2));
      expect(series.points[0].revenue, 31);
      expect(series.points[1].revenue, 56);
    });

    test('ignores points outside the requested range', () {
      final series = SalesTrendSeries.fromDailyPoints(
        _daily(DateTime(2026, 3, 1), 10, 5),
        granularity: SalesTrendGranularity.day,
        from: DateTime(2026, 3, 3),
        to: DateTime(2026, 3, 5),
      );

      expect(series.points, hasLength(3));
      expect(series.total, 15);
    });

    test('computes a period-over-period change when prior data exists', () {
      final series = SalesTrendSeries.fromDailyPoints(
        [
          // Prior window: 4 days at 100 = 400.
          ..._daily(DateTime(2026, 3, 1), 4, 100),
          // Current window: 4 days at 150 = 600.
          ..._daily(DateTime(2026, 3, 5), 4, 150),
        ],
        granularity: SalesTrendGranularity.day,
        from: DateTime(2026, 3, 5),
        to: DateTime(2026, 3, 8),
      );

      expect(series.previousTotal, 400);
      expect(series.changeFraction, closeTo(0.5, 1e-9));
    });

    test(
      'refuses to report a change when the data does not reach the prior '
      'window',
      () {
        final series = SalesTrendSeries.fromDailyPoints(
          _daily(DateTime(2026, 3, 5), 4, 150),
          granularity: SalesTrendGranularity.day,
          from: DateTime(2026, 3, 5),
          to: DateTime(2026, 3, 8),
        );

        expect(series.previousTotal, isNull);
        expect(series.changeFraction, isNull);
      },
    );

    test('reports the window the data actually covered', () {
      final series = SalesTrendSeries.fromDailyPoints(
        _daily(DateTime(2026, 3, 10), 3, 20),
        granularity: SalesTrendGranularity.day,
        from: DateTime(2026, 1, 1),
        to: DateTime(2026, 3, 12),
      );

      expect(series.coverageFrom, DateTime(2026, 3, 10));
      expect(series.coverageTo, DateTime(2026, 3, 12));
    });

    test('is empty when every bucket is zero', () {
      final series = SalesTrendSeries.fromDailyPoints(
        _daily(DateTime(2026, 3, 1), 3, 0),
        granularity: SalesTrendGranularity.day,
        from: DateTime(2026, 3, 1),
        to: DateTime(2026, 3, 3),
      );

      expect(series.isEmpty, isTrue);
    });
  });

  group('SalesTrendGranularity.forRange', () {
    test('picks a bucket size that keeps a custom range readable', () {
      expect(
        SalesTrendGranularityLabel.forRange(
          DateTime(2026, 3, 1),
          DateTime(2026, 3, 20),
        ),
        SalesTrendGranularity.day,
      );
      expect(
        SalesTrendGranularityLabel.forRange(
          DateTime(2026, 1, 1),
          DateTime(2026, 5, 1),
        ),
        SalesTrendGranularity.week,
      );
      expect(
        SalesTrendGranularityLabel.forRange(
          DateTime(2024, 1, 1),
          DateTime(2026, 1, 1),
        ),
        SalesTrendGranularity.month,
      );
      expect(
        SalesTrendGranularityLabel.forRange(
          DateTime(2016, 1, 1),
          DateTime(2026, 1, 1),
        ),
        SalesTrendGranularity.year,
      );
    });
  });
}
