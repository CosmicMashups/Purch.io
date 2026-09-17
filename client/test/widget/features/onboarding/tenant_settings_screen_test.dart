import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';
import 'package:purch_client/core/db/db_providers.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';
import 'package:purch_client/features/onboarding/presentation/screens/tenant_settings_screen.dart';

import '../../../helpers/fake_onboarding_repository.dart';

Widget _wrap(FakeOnboardingRepository repository, AppDatabase database) {
  return ProviderScope(
    overrides: [
      onboardingRepositoryProvider.overrideWithValue(repository),
      // The screen's notifier mirrors branding into the local Drift cache
      // (that's what drives the runtime theme), so give it an in-memory DB
      // rather than the real on-device file.
      appDatabaseProvider.overrideWithValue(database),
    ],
    child: const MaterialApp(home: TenantSettingsScreen()),
  );
}

void main() {
  testWidgets('loads and displays the current settings', (tester) async {
    final repository = FakeOnboardingRepository();
    final database = AppDatabase.forTesting(NativeDatabase.memory());
    addTearDown(database.close);
    await tester.pumpWidget(_wrap(repository, database));
    await tester.pumpAndSettle();

    expect(find.text('Branding'), findsOneWidget);
    expect(find.text('BIR / Compliance'), findsOneWidget);
    expect(find.text('Require a barcode for every item'), findsOneWidget);
  });

  testWidgets('saving branding sends the entered values', (tester) async {
    final repository = FakeOnboardingRepository();
    final database = AppDatabase.forTesting(NativeDatabase.memory());
    addTearDown(database.close);
    await tester.pumpWidget(_wrap(repository, database));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextField, 'Logo URL'),
      'https://example.com/logo.png',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Background color (e.g. #F8FAFC)'),
      '#123456',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Brand Primary (e.g. #0F766E)'),
      '#234567',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Primary text color (e.g. #0F172A)'),
      '#345678',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Secondary text color (e.g. #475569)'),
      '#456789',
    );
    // The branding card now holds four colour fields, so the save button
    // starts below the fold in an 800x600 test viewport.
    await tester.ensureVisible(find.text('Save Branding'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Save Branding'));
    await tester.pumpAndSettle();

    expect(repository.settings.brandingLogoUrl, 'https://example.com/logo.png');
    expect(repository.settings.brandingBackgroundColorHex, '#123456');
    expect(repository.settings.brandingAccentColorHex, '#234567');
    expect(repository.settings.brandingPrimaryTextColorHex, '#345678');
    expect(repository.settings.brandingSecondaryTextColorHex, '#456789');
  });

  testWidgets('all four branding color fields and derived container are rendered', (tester) async {
    final repository = FakeOnboardingRepository();
    final database = AppDatabase.forTesting(NativeDatabase.memory());
    addTearDown(database.close);
    await tester.pumpWidget(_wrap(repository, database));
    await tester.pumpAndSettle();

    expect(
      find.widgetWithText(TextField, 'Background color (e.g. #F8FAFC)'),
      findsOneWidget,
    );
    expect(
      find.widgetWithText(TextField, 'Brand Primary (e.g. #0F766E)'),
      findsOneWidget,
    );
    expect(
      find.textContaining('Derived Primary Container:'),
      findsOneWidget,
    );
    expect(
      find.widgetWithText(TextField, 'Primary text color (e.g. #0F172A)'),
      findsOneWidget,
    );
    expect(
      find.widgetWithText(TextField, 'Secondary text color (e.g. #475569)'),
      findsOneWidget,
    );
  });

  testWidgets('toggling the barcode requirement saves immediately', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository();
    final database = AppDatabase.forTesting(NativeDatabase.memory());
    addTearDown(database.close);
    await tester.pumpWidget(_wrap(repository, database));
    await tester.pumpAndSettle();

    final barcodeSwitch = find.widgetWithText(
      SwitchListTile,
      'Require a barcode for every item',
    );
    await tester.ensureVisible(barcodeSwitch);
    await tester.tap(barcodeSwitch);
    await tester.pumpAndSettle();

    expect(repository.settings.requiresBarcodePerItem, isTrue);
  });

  testWidgets('toggling the credit ledger setting saves immediately', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository();
    final database = AppDatabase.forTesting(NativeDatabase.memory());
    addTearDown(database.close);
    await tester.pumpWidget(_wrap(repository, database));
    await tester.pumpAndSettle();

    final creditLedgerSwitch = find.widgetWithText(
      SwitchListTile,
      'Offer utang / credit sales',
    );
    await tester.ensureVisible(creditLedgerSwitch);
    await tester.tap(creditLedgerSwitch);
    await tester.pumpAndSettle();

    expect(repository.settings.creditLedgerEnabled, isTrue);
  });
}
