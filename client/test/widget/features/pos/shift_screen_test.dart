import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/domain/shift_models.dart';
import 'package:purch_client/features/pos/presentation/providers/shift_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/shift_screen.dart';

import '../../../helpers/fake_shift_repository.dart';

Widget _wrap(FakeShiftRepository repository) {
  return ProviderScope(
    overrides: [shiftRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: ShiftScreen()),
  );
}

void main() {
  testWidgets('shows the open-shift form when no shift is open', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeShiftRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No shift is open on this device.'), findsOneWidget);
  });

  testWidgets('opening a shift with a starting float shows the shift detail', (
    tester,
  ) async {
    final repository = FakeShiftRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, '1000');
    await tester.pump();
    final openButton = find.widgetWithText(FilledButton, 'Open Shift');
    await tester.tap(openButton);
    await tester.pumpAndSettle();

    expect(repository.lastOpenShiftRequest?.openingCashAmount, 1000);
    expect(find.text('Shift open'), findsOneWidget);
    // formatCurrency adds the thousands separator now used everywhere else in the app.
    expect(find.text('Opening cash: ₱1,000.00'), findsOneWidget);
  });

  testWidgets('closing a shift with a matching count needs no PIN', (
    tester,
  ) async {
    final fakeOpenShift = await FakeShiftRepository().openShift(
      const OpenShiftRequest(openingCashAmount: 1000),
    );
    final repository = FakeShiftRepository(initialShift: fakeOpenShift);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    final closingCashField = find.widgetWithText(
      TextField,
      'Closing cash count',
    );
    await tester.enterText(closingCashField, '1000');
    await tester.pump();
    final closeButton = find.widgetWithText(FilledButton, 'Close Shift');
    await tester.tap(closeButton);
    await tester.pumpAndSettle();

    expect(repository.lastCloseShiftRequest?.closingCashAmount, 1000);
    expect(find.text('Shift closed'), findsOneWidget);
    expect(find.text('Matched exactly'), findsOneWidget);
  });

  testWidgets(
    'closing a shift with a mismatched count and a manager PIN shows the approver',
    (tester) async {
      final fakeOpenShift = await FakeShiftRepository().openShift(
        const OpenShiftRequest(openingCashAmount: 1000),
      );
      final repository = FakeShiftRepository(initialShift: fakeOpenShift);
      await tester.pumpWidget(_wrap(repository));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextField, 'Closing cash count'),
        '950',
      );
      await tester.enterText(
        find.widgetWithText(TextField, 'Manager/Admin PIN'),
        '5678',
      );
      await tester.pump();
      final closeButton = find.widgetWithText(FilledButton, 'Close Shift');
      await tester.tap(closeButton);
      await tester.pumpAndSettle();

      expect(repository.lastCloseShiftRequest?.approverPin, '5678');
      expect(find.text('Short by ₱50.00'), findsOneWidget);
      expect(find.text('Approved by: Manager Mae'), findsOneWidget);
    },
  );

  testWidgets('tapping Done after closing returns to the open-shift form', (
    tester,
  ) async {
    final fakeOpenShift = await FakeShiftRepository().openShift(
      const OpenShiftRequest(openingCashAmount: 1000),
    );
    final repository = FakeShiftRepository(initialShift: fakeOpenShift);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextField, 'Closing cash count'),
      '1000',
    );
    await tester.pump();
    final closeButton = find.widgetWithText(FilledButton, 'Close Shift');
    await tester.tap(closeButton);
    await tester.pumpAndSettle();

    final doneButton = find.widgetWithText(FilledButton, 'Done');
    await tester.tap(doneButton);
    await tester.pumpAndSettle();

    expect(find.text('No shift is open on this device.'), findsOneWidget);
  });
}
