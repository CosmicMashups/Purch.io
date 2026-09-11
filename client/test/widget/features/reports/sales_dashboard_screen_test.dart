import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/reports/domain/sales_dashboard_models.dart';
import 'package:purch_client/features/reports/presentation/providers/reports_providers.dart';
import 'package:purch_client/features/reports/presentation/screens/sales_dashboard_screen.dart';

import '../../../helpers/fake_reports_repository.dart';

Widget _wrap(FakeReportsRepository repository) {
  return ProviderScope(
    overrides: [reportsRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: SalesDashboardScreen()),
  );
}

void main() {
  testWidgets('shows empty states when there is no sales data yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeReportsRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No sales in the last 30 days yet.'), findsOneWidget);
    expect(find.text('No branch data yet.'), findsOneWidget);
  });

  testWidgets('shows revenue cards, top-selling items, and branch comparison', (
    tester,
  ) async {
    final dashboard = SalesDashboard(
      revenueToday: 500,
      revenueLast7Days: 2500,
      revenueLast30Days: 9000,
      trend: [DailyRevenuePoint(date: DateTime(2026, 1, 1), revenue: 500)],
      topSellingItems: const [
        TopSellingItem(
          itemId: 'item-1',
          itemName: 'Rice Meal',
          quantitySold: 10,
          revenue: 850,
        ),
      ],
      branchComparison: const [
        BranchRevenue(
          branchId: 'branch-1',
          branchName: 'Main Branch',
          revenue: 8000,
        ),
      ],
    );

    await tester.pumpWidget(
      _wrap(FakeReportsRepository(salesDashboard: dashboard)),
    );
    await tester.pumpAndSettle();

    expect(find.text('₱500.00'), findsWidgets);
    expect(find.text('Rice Meal'), findsOneWidget);
    expect(find.text('Main Branch'), findsOneWidget);
    expect(find.text('₱8000.00'), findsOneWidget);
  });
}
