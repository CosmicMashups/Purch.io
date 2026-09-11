import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/reports/domain/inventory_report_models.dart';
import 'package:purch_client/features/reports/presentation/providers/reports_providers.dart';
import 'package:purch_client/features/reports/presentation/screens/inventory_reports_screen.dart';
import 'package:purch_client/features/inventory/domain/inventory_movement_models.dart';

import '../../../helpers/fake_reports_repository.dart';

Widget _wrap(FakeReportsRepository repository) {
  return ProviderScope(
    overrides: [reportsRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: InventoryReportsScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no movements yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeReportsRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No stock movements in this range yet.'), findsOneWidget);
  });

  testWidgets('shows non-zero movement type rows from the summary', (
    tester,
  ) async {
    final now = DateTime.now();
    final summary = MovementSummary(
      from: now,
      to: now,
      byType: const [
        MovementTypeSummary(
          type: MovementType.stockIn,
          totalQuantity: 20,
          movementCount: 1,
        ),
        MovementTypeSummary(
          type: MovementType.stockOut,
          totalQuantity: 0,
          movementCount: 0,
        ),
      ],
    );

    await tester.pumpWidget(
      _wrap(FakeReportsRepository(movementSummary: summary)),
    );
    await tester.pumpAndSettle();

    expect(find.text('Stock-In'), findsOneWidget);
    expect(find.text('Stock-Out'), findsNothing);
  });

  testWidgets('generating the CSV export shows its content', (tester) async {
    await tester.pumpWidget(
      _wrap(FakeReportsRepository(lowStockCsv: 'Item,Stock On Hand\nRice,2')),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Generate CSV'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Rice,2'), findsOneWidget);
  });
}
