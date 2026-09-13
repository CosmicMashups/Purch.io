import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';
import 'package:purch_client/features/onboarding/presentation/screens/bootstrap_screen.dart';

import '../../../helpers/fake_onboarding_repository.dart';

Widget _wrap(FakeOnboardingRepository repository) {
  return ProviderScope(
    overrides: [onboardingRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: BootstrapScreen()),
  );
}

/// Step 1 -> Step 2 of the wizard: fills the business name and taps
/// Continue, then taps Continue again to leave the branch step's default
/// branch name untouched.
Future<void> _advanceToAdminStep(
  WidgetTester tester, {
  required String businessName,
}) async {
  await tester.enterText(
    find.widgetWithText(TextFormField, 'Business name'),
    businessName,
  );
  await tester.tap(find.text('Continue'));
  await tester.pumpAndSettle();

  await tester.tap(find.text('Continue'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    'submitting step 1 with an empty business name shows a validation error and never calls the repository',
    (tester) async {
      final repository = FakeOnboardingRepository();
      await tester.pumpWidget(_wrap(repository));

      await tester.tap(find.text('Continue'));
      await tester.pump();

      expect(find.text('Required'), findsWidgets);
      expect(repository.lastBootstrapRequest, isNull);
    },
  );

  testWidgets('a successful bootstrap shows the device pairing code', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository();
    await tester.pumpWidget(_wrap(repository));

    await _advanceToAdminStep(tester, businessName: "Ana's Store");

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Your name'),
      'Ana Reyes',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Choose a PIN'),
      '1234',
    );
    await tester.tap(find.text('Create Business'));
    await tester.pumpAndSettle();

    expect(find.text('ABCD1234'), findsOneWidget);
    expect(repository.lastBootstrapRequest?.tenantName, "Ana's Store");
  });

  testWidgets('a rejected bootstrap shows the failure message', (tester) async {
    final repository = FakeOnboardingRepository(
      bootstrapFailure: const ConflictFailure(
        'That business name is already taken.',
      ),
    );
    await tester.pumpWidget(_wrap(repository));

    await _advanceToAdminStep(tester, businessName: 'Duplicate');

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Your name'),
      'Ana Reyes',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Choose a PIN'),
      '1234',
    );
    await tester.tap(find.text('Create Business'));
    await tester.pumpAndSettle();

    expect(find.text('That business name is already taken.'), findsOneWidget);
  });
}
