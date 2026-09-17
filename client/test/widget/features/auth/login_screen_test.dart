import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/auth/presentation/providers/auth_providers.dart';
import 'package:purch_client/features/auth/presentation/screens/login_screen.dart';

import '../../../helpers/fake_auth_repository.dart';

Widget _wrap(Widget child, {required FakeAuthRepository repository}) {
  return ProviderScope(
    overrides: [authRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp(home: child),
  );
}

void main() {
  testWidgets(
    'submitting with empty fields shows validation errors and never calls the repository',
    (tester) async {
      final repository = FakeAuthRepository();
      await tester.pumpWidget(
        _wrap(LoginScreen(onLoggedIn: () {}), repository: repository),
      );

      await tester.ensureVisible(find.text('Log In'));
      await tester.tap(find.text('Log In'));
      await tester.pump();

      expect(find.text('Required'), findsNWidgets(2));
      expect(repository.lastPairingCode, isNull);
    },
  );

  testWidgets(
    'a successful login calls onLoggedIn with the entered credentials',
    (tester) async {
      final repository = FakeAuthRepository();
      var loggedIn = false;

      await tester.pumpWidget(
        _wrap(
          LoginScreen(onLoggedIn: () => loggedIn = true),
          repository: repository,
        ),
      );

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Device pairing code'),
        'DEVICE-1',
      );
      await tester.enterText(find.widgetWithText(TextFormField, 'PIN'), '1234');
      await tester.ensureVisible(find.text('Log In'));
      await tester.tap(find.text('Log In'));
      await tester.pumpAndSettle();

      expect(repository.lastPairingCode, 'DEVICE-1');
      expect(repository.lastPin, '1234');
      expect(loggedIn, isTrue);
    },
  );

  testWidgets(
    'a rejected login shows the failure message and does not call onLoggedIn',
    (tester) async {
      final repository = FakeAuthRepository(
        failureToThrow: const UnauthorizedFailure(
          'The device pairing code or PIN was not recognized.',
        ),
      );
      var loggedIn = false;

      await tester.pumpWidget(
        _wrap(
          LoginScreen(onLoggedIn: () => loggedIn = true),
          repository: repository,
        ),
      );

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Device pairing code'),
        'DEVICE-1',
      );
      await tester.enterText(find.widgetWithText(TextFormField, 'PIN'), '9999');
      await tester.ensureVisible(find.text('Log In'));
      await tester.tap(find.text('Log In'));
      await tester.pumpAndSettle();

      expect(
        find.text('The device pairing code or PIN was not recognized.'),
        findsOneWidget,
      );
      expect(loggedIn, isFalse);
    },
  );

  testWidgets(
    'fits above the fold in standard 9:16 phone viewport without scrolling',
    (tester) async {
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

      // "Log In" button and "Device Sign In" title are directly on screen
      expect(find.text('Device Sign In'), findsOneWidget);
      expect(find.text('Log In'), findsOneWidget);
      expect(find.text('Sign in as admin instead'), findsOneWidget);
      expect(find.text('Set up a new business'), findsOneWidget);
      expect(find.byIcon(Icons.dns_outlined), findsOneWidget);

      // Tap directly without scroll
      await tester.tap(find.text('Log In'));
      await tester.pump();
      expect(find.text('Required'), findsNWidgets(2));
    },
  );
}

