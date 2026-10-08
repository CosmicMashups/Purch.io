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

  // The admin step now has email and password too, so give it room to show every field.
  tester.view.physicalSize = const Size(800, 1800);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(() {
    tester.view.resetPhysicalSize();
    tester.view.resetDevicePixelRatio();
  });
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

  testWidgets('a successful bootstrap tells the owner to sign in with the email and password', (
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
      find.widgetWithText(TextFormField, 'Email'),
      'ana@example.com',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Password'),
      'correct horse battery',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Choose a PIN'),
      '123456',
    );
    await tester.tap(find.byType(Checkbox));
    await tester.pump();
    await tester.tap(find.text('Create Business'));
    await tester.pumpAndSettle();

    expect(find.text('Setup complete'), findsOneWidget);
    expect(repository.lastBootstrapRequest?.tenantName, "Ana's Store");
    expect(repository.lastBootstrapRequest?.adminEmail, 'ana@example.com');
    expect(repository.lastBootstrapRequest?.adminPassword, 'correct horse battery');
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
      find.widgetWithText(TextFormField, 'Email'),
      'ana@example.com',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Password'),
      'correct horse battery',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Choose a PIN'),
      '123456',
    );
    await tester.tap(find.byType(Checkbox));
    await tester.pump();
    await tester.tap(find.text('Create Business'));
    await tester.pumpAndSettle();

    expect(find.text('That business name is already taken.'), findsOneWidget);
  });

  testWidgets(
    'reaching the admin step without checking the legal agreement shows an error and never submits',
    (tester) async {
      final repository = FakeOnboardingRepository();
      await tester.pumpWidget(_wrap(repository));

      await _advanceToAdminStep(tester, businessName: "Ana's Store");

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Your name'),
        'Ana Reyes',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'ana@example.com',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'),
        'correct horse battery',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Choose a PIN'),
        '123456',
      );
      await tester.tap(find.text('Create Business'));
      await tester.pump();

      expect(
        find.text('Please review and accept to continue.'),
        findsOneWidget,
      );
      expect(repository.lastBootstrapRequest, isNull);
    },
  );
}
