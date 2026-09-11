import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';
import 'package:purch_client/features/onboarding/presentation/screens/tenant_settings_screen.dart';

import '../../../helpers/fake_onboarding_repository.dart';

Widget _wrap(FakeOnboardingRepository repository) {
  return ProviderScope(
    overrides: [onboardingRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: TenantSettingsScreen()),
  );
}

void main() {
  testWidgets('loads and displays the current settings', (tester) async {
    final repository = FakeOnboardingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Branding'), findsOneWidget);
    expect(find.text('BIR / Compliance'), findsOneWidget);
    expect(find.text('Require a barcode for every item'), findsOneWidget);
  });

  testWidgets('saving branding sends the entered values', (tester) async {
    final repository = FakeOnboardingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextField, 'Logo URL'),
      'https://example.com/logo.png',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Theme color (e.g. #4F46E5)'),
      '#123456',
    );
    await tester.tap(find.text('Save Branding'));
    await tester.pumpAndSettle();

    expect(repository.settings.brandingLogoUrl, 'https://example.com/logo.png');
    expect(repository.settings.brandingThemeColorHex, '#123456');
  });

  testWidgets('toggling the barcode requirement saves immediately', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.byType(SwitchListTile));
    await tester.tap(find.byType(SwitchListTile));
    await tester.pumpAndSettle();

    expect(repository.settings.requiresBarcodePerItem, isTrue);
  });
}
