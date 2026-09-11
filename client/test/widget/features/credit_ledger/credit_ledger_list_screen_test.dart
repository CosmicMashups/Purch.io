import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/credit_ledger/domain/credit_ledger_models.dart';
import 'package:purch_client/features/credit_ledger/presentation/providers/credit_ledger_providers.dart';
import 'package:purch_client/features/credit_ledger/presentation/screens/credit_ledger_list_screen.dart';

import '../../../helpers/fake_credit_ledger_repository.dart';

Widget _wrap(FakeCreditLedgerRepository repository) {
  return ProviderScope(
    overrides: [creditLedgerRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: CreditLedgerListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no customer accounts yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeCreditLedgerRepository()));
    await tester.pumpAndSettle();

    expect(
      find.text('No customer accounts yet — tap + to add one.'),
      findsOneWidget,
    );
  });

  testWidgets(
    'shows a ledger with a Record Payment action when it has a balance',
    (tester) async {
      const ledger = CustomerCreditLedger(
        id: 'ledger-1',
        customerFullName: 'Juan Dela Cruz',
        customerPhoneNumber: '09171234567',
        customerAddress: null,
        balance: 300,
        creditLimit: 1000,
        dueDate: null,
        isActive: true,
      );

      await tester.pumpWidget(
        _wrap(FakeCreditLedgerRepository(ledgers: [ledger])),
      );
      await tester.pumpAndSettle();

      expect(find.text('Juan Dela Cruz'), findsOneWidget);
      expect(find.text('Record Payment'), findsOneWidget);
    },
  );

  testWidgets(
    'recording a payment within the balance calls the repository and closes the dialog',
    (tester) async {
      const ledger = CustomerCreditLedger(
        id: 'ledger-1',
        customerFullName: 'Juan Dela Cruz',
        customerPhoneNumber: '09171234567',
        customerAddress: null,
        balance: 300,
        creditLimit: 1000,
        dueDate: null,
        isActive: true,
      );
      final repository = FakeCreditLedgerRepository(ledgers: [ledger]);

      await tester.pumpWidget(_wrap(repository));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Record Payment'));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextFormField), '100');
      await tester.pump();
      await tester.tap(find.text('Record'));
      await tester.pumpAndSettle();

      expect(repository.lastPaymentRequest?.amount, 100);
      expect(find.byType(AlertDialog), findsNothing);
    },
  );
}
