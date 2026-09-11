import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/onboarding/domain/branch_models.dart';
import 'package:purch_client/features/onboarding/domain/hardware_enums.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';
import 'package:purch_client/features/onboarding/presentation/screens/manual_gcash_qr_settings_screen.dart';

import '../../../helpers/fake_onboarding_repository.dart';

const _branch = Branch(
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
    child: MaterialApp(home: ManualGcashQrSettingsScreen(branch: _branch)),
  );
}

void main() {
  testWidgets('saving the QR settings sends the entered values', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository(initialBranches: [_branch]);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextField, 'QR code image URL'),
      'https://cdn.example.com/gcash-qr.png',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'GCash account name'),
      'Ana Dela Cruz',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'GCash number'),
      '0917-000-0000',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    expect(
      repository.branches.single.manualGcashQrImageUrl,
      'https://cdn.example.com/gcash-qr.png',
    );
    expect(repository.branches.single.manualGcashAccountName, 'Ana Dela Cruz');
  });
}
