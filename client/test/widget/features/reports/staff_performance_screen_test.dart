import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/reports/domain/staff_performance_models.dart';
import 'package:purch_client/features/reports/presentation/providers/reports_providers.dart';
import 'package:purch_client/features/reports/presentation/screens/staff_performance_screen.dart';

import '../../../helpers/fake_reports_repository.dart';

Widget _wrap(FakeReportsRepository repository) {
  return ProviderScope(
    overrides: [reportsRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: StaffPerformanceScreen()),
  );
}

void main() {
  testWidgets('shows empty states when there is no data yet', (tester) async {
    await tester.pumpWidget(_wrap(FakeReportsRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No completed sales in this range yet.'), findsOneWidget);
    expect(find.text('No closed shifts in this range yet.'), findsOneWidget);
  });

  testWidgets('shows sales per cashier and flags shifts with a variance', (
    tester,
  ) async {
    final report = const StaffPerformanceReport(
      sales: [
        StaffSalesSummary(
          staffUserId: 'user-1',
          staffName: 'Admin User',
          transactionCount: 3,
          totalSales: 450,
        ),
      ],
      shiftAttendance: [
        StaffShiftAttendance(
          staffUserId: 'user-1',
          staffName: 'Admin User',
          shiftsOpened: 2,
          shiftsWithVariance: 1,
        ),
      ],
    );

    await tester.pumpWidget(
      _wrap(FakeReportsRepository(staffPerformance: report)),
    );
    await tester.pumpAndSettle();

    expect(find.text('Admin User'), findsNWidgets(2));
    expect(find.text('3 sale(s)'), findsOneWidget);
    expect(find.text('₱450.00'), findsOneWidget);
    expect(find.text('1 with variance'), findsOneWidget);
  });
}
