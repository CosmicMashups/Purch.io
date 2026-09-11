import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/item_batches_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(
      home: ItemBatchesScreen(itemId: 'item-1', itemName: 'Rice'),
    ),
  );
}

void main() {
  testWidgets('shows an empty state when no stock has been received yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(
      find.text('No stock received yet — tap + to receive some.'),
      findsOneWidget,
    );
  });

  testWidgets('receiving stock from the + button shows the new batch', (
    tester,
  ) async {
    final repository = FakeCatalogRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Lot number'),
      'LOT-001',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Quantity received'),
      '50',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Receive Stock'));
    await tester.pumpAndSettle();

    expect(find.text('Lot LOT-001'), findsOneWidget);
    expect(repository.lastReceiveBatchRequest?.quantityReceived, 50);
  });

  testWidgets('a zero quantity is rejected client-side', (tester) async {
    final repository = FakeCatalogRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Lot number'),
      'LOT-002',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Quantity received'),
      '0',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Receive Stock'));
    await tester.pump();

    expect(find.text('Enter a quantity greater than zero'), findsOneWidget);
    expect(repository.lastReceiveBatchRequest, isNull);
  });
}
