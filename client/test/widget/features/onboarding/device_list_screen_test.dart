import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/onboarding/domain/branch_models.dart';
import 'package:purch_client/features/onboarding/domain/hardware_enums.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';
import 'package:purch_client/features/onboarding/presentation/screens/device_list_screen.dart';

import '../../../helpers/fake_onboarding_repository.dart';

const _mainBranch = Branch(
  id: 'branch-1',
  name: 'Main Branch',
  address: null,
  receiptPrinterProfile: ReceiptPrinterProfile.none,
  cashDrawerEnabled: false,
  cashDrawerPolicy: CashDrawerPolicy.kickOnSaleOnly,
  manualGcashQrImageUrl: null,
  manualGcashAccountName: null,
  manualGcashAccountNumber: null,
);

Widget _wrap(FakeOnboardingRepository repository) {
  return ProviderScope(
    overrides: [onboardingRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: DeviceListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no devices yet', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository(initialBranches: [_mainBranch]);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(
      find.text('No paired devices yet — tap + to pair one.'),
      findsOneWidget,
    );
  });

  testWidgets('making a device shows its one-time pairing code', (tester) async {
    final repository = FakeOnboardingRepository(initialBranches: [_mainBranch]);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Device name'),
      'Front Counter',
    );
    await tester.tap(find.byType(DropdownButtonFormField<Branch>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Main Branch').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Make pairing code'));
    await tester.pumpAndSettle();

    expect(find.text('CODE1'), findsOneWidget);
    expect(repository.lastCreateDeviceRequest?.branchId, 'branch-1');
    expect(repository.lastCreateDeviceRequest?.name, 'Front Counter');

    await tester.tap(find.text('Done'));
    await tester.pumpAndSettle();
    expect(find.text('Front Counter'), findsOneWidget);
    expect(find.text('Waiting for its code'), findsOneWidget);
  });
}
