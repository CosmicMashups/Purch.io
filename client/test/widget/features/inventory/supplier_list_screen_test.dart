import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/inventory/domain/supplier_models.dart';
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
    await tester.ensureVisible(find.widgetWithText(FilledButton, 'Add Supplier'));
    await tester.tap(find.widgetWithText(FilledButton, 'Add Supplier'));
    await tester.pumpAndSettle();

    expect(repository.lastCreateRequest?.name, 'Acme Distribution');
    expect(find.text('Acme Distribution'), findsOneWidget);
  });

  testWidgets('tapping a supplier edits it, including its contacts', (
    tester,
  ) async {
    final repository = FakeSupplierRepository(
      initialSuppliers: [
        const Supplier(
          id: 's1',
          name: 'Metro Foods',
          contactInfo: null,
          isActive: true,
          specialization: 'Dairy',
          contacts: [
            SupplierContact(
              contactPerson: 'Ana',
              modes: ['Call'],
              numbers: ['0917 111 2222', '0918 333 4444'],
              emails: ['ana@metro.test'],
            ),
          ],
        ),
      ],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Metro Foods'));
    await tester.pumpAndSettle();

    expect(find.text('Edit Supplier'), findsOneWidget);
    expect(find.text('0918 333 4444'), findsOneWidget);

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Supplier name'),
      'Metro Foods Inc',
    );
    await tester.ensureVisible(find.widgetWithText(FilledButton, 'Save Changes'));
    await tester.tap(find.widgetWithText(FilledButton, 'Save Changes'));
    await tester.pumpAndSettle();

    expect(repository.lastUpdateRequest?.name, 'Metro Foods Inc');
    expect(repository.lastUpdateRequest?.contacts.single.numbers, hasLength(2));
    expect(find.text('Metro Foods Inc'), findsOneWidget);
  });
}
