import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/reports/domain/department_sales_models.dart';
import 'package:purch_client/features/reports/presentation/providers/reports_providers.dart';
import 'package:purch_client/features/reports/presentation/screens/department_sales_screen.dart';

import '../../../helpers/fake_reports_repository.dart';

Widget _wrap(FakeReportsRepository repository) {
  return ProviderScope(
    overrides: [reportsRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: DepartmentSalesScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no sales yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeReportsRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No sales in this range yet.'), findsOneWidget);
  });

  testWidgets('shows a row per department plus a general row and the total', (
    tester,
  ) async {
    const rows = [
      DepartmentSalesSummary(
        departmentId: 'dept-1',
        departmentName: 'Concessionaire Stall',
        revenue: 200,
      ),
      DepartmentSalesSummary(
        departmentId: null,
        departmentName: 'General (no department)',
        revenue: 100,
      ),
    ];

    await tester.pumpWidget(
      _wrap(FakeReportsRepository(departmentSales: rows)),
    );
    await tester.pumpAndSettle();

    expect(find.text('Concessionaire Stall'), findsOneWidget);
    expect(find.text('General (no department)'), findsOneWidget);
    expect(find.text('₱300.00'), findsOneWidget);
  });
}
