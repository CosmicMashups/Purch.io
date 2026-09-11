import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/inventory/presentation/providers/supplier_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/supplier_list_screen.dart';

import '../../../helpers/fake_supplier_repository.dart';

Widget _wrap(FakeSupplierRepository repository) {
  return ProviderScope(
    overrides: [supplierRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: SupplierListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no suppliers', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeSupplierRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No suppliers yet — tap + to add one.'), findsOneWidget);
  });

  testWidgets('adding a supplier from the + button refreshes the list', (
    tester,
  ) async {
    final repository = FakeSupplierRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Supplier name'),
      'Acme Distribution',
    );
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Add Supplier'));
    await tester.pumpAndSettle();

    expect(repository.lastCreateRequest?.name, 'Acme Distribution');
    expect(find.text('Acme Distribution'), findsOneWidget);
  });
}
