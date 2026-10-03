import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/auth/domain/auth_models.dart';
import 'package:purch_client/features/auth/presentation/providers/auth_providers.dart';
import 'package:purch_client/features/auth/presentation/screens/login_screen.dart';

import '../../../helpers/fake_auth_repository.dart';

Widget _wrap(Widget child, {required FakeAuthRepository repository}) {
  return ProviderScope(
    overrides: [authRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp(home: child),
  );
}

Future<void> _fillAndSubmit(WidgetTester tester) async {
  await tester.enterText(
    find.widgetWithText(TextFormField, 'Email'),
    'ana@example.com',
  );
  await tester.enterText(
    find.widgetWithText(TextFormField, 'Password'),
    'correct horse battery',
  );
  await tester.ensureVisible(find.widgetWithText(FilledButton, 'Sign in'));
  await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    'submitting with empty fields shows validation errors and never calls the repository',
    (tester) async {
      final repository = FakeAuthRepository();
      await tester.pumpWidget(
        _wrap(LoginScreen(onLoggedIn: () {}), repository: repository),
      );

      await tester.ensureVisible(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pump();

      expect(find.text('Required'), findsNWidgets(2));
      expect(repository.lastEmail, isNull);
    },
  );

  testWidgets('a successful sign-in calls onLoggedIn with the entered details', (
    tester,
  ) async {
    final repository = FakeAuthRepository();
    var loggedIn = false;
    await tester.pumpWidget(
      _wrap(
        LoginScreen(onLoggedIn: () => loggedIn = true),
        repository: repository,
      ),
    );

    await _fillAndSubmit(tester);

    expect(repository.lastEmail, 'ana@example.com');
    expect(repository.lastPassword, 'correct horse battery');
    expect(loggedIn, isTrue);
  });

  testWidgets(
    'a person in several businesses picks one, then signs in to it',
    (tester) async {
      final repository = FakeAuthRepository(
        businessesToChooseFrom: const [
          BusinessChoice(tenantId: 't1', name: 'First Store'),
          BusinessChoice(tenantId: 't2', name: 'Second Store'),
        ],
      );
      var loggedIn = false;
      await tester.pumpWidget(
        _wrap(
          LoginScreen(onLoggedIn: () => loggedIn = true),
          repository: repository,
        ),
      );

      await _fillAndSubmit(tester);
      expect(loggedIn, isFalse);
      expect(find.text('Second Store'), findsOneWidget);

      await tester.tap(find.text('Second Store'));
      await tester.pumpAndSettle();

      expect(repository.lastTenantId, 't2');
      expect(loggedIn, isTrue);
    },
  );

  testWidgets(
    'a rejected sign-in shows the failure message and does not call onLoggedIn',
    (tester) async {
      final repository = FakeAuthRepository(
        failureToThrow: const UnauthorizedFailure('Incorrect sign-in details.'),
      );
      var loggedIn = false;
      await tester.pumpWidget(
        _wrap(
          LoginScreen(onLoggedIn: () => loggedIn = true),
          repository: repository,
        ),
      );

      await _fillAndSubmit(tester);

      expect(find.text('Incorrect sign-in details.'), findsOneWidget);
      expect(loggedIn, isFalse);
    },
  );

  testWidgets('fits in a standard phone viewport and offers pairing and setup', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(360, 640);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(() {
      tester.view.resetPhysicalSize();
      tester.view.resetDevicePixelRatio();
    });

    final repository = FakeAuthRepository();
    await tester.pumpWidget(
      _wrap(LoginScreen(onLoggedIn: () {}), repository: repository),
    );
    await tester.pumpAndSettle();

    expect(find.text('Use your email and password'), findsOneWidget);
    expect(find.text('Pair this device with a code'), findsOneWidget);
    expect(find.text('Set up a new business'), findsOneWidget);
    expect(find.byIcon(Icons.dns_outlined), findsOneWidget);
  });
}
