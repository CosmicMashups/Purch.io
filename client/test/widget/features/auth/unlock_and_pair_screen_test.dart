import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/auth/domain/auth_models.dart';
import 'package:purch_client/features/auth/presentation/providers/auth_providers.dart';
import 'package:purch_client/features/auth/presentation/screens/pair_device_screen.dart';
import 'package:purch_client/features/auth/presentation/screens/unlock_screen.dart';

import '../../../helpers/fake_auth_repository.dart';

Widget _wrap(Widget child, {required FakeAuthRepository repository}) {
  return ProviderScope(
    overrides: [authRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp(home: child),
  );
}

const _carlo = RosterPerson(
  membershipId: 'm1',
  name: 'Carlo Cashier',
  role: 2,
  hasPin: true,
);

Future<void> _typePin(WidgetTester tester, String pin) async {
  for (final digit in pin.split('')) {
    await tester.tap(find.widgetWithText(OutlinedButton, digit));
    await tester.pump();
  }
}

void main() {
  group('UnlockScreen', () {
    testWidgets('a person picks their name, types their PIN and unlocks', (
      tester,
    ) async {
      final repository = FakeAuthRepository(people: const [_carlo]);
      var unlocked = false;
      await tester.pumpWidget(
        _wrap(
          UnlockScreen(onUnlocked: () => unlocked = true),
          repository: repository,
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Carlo Cashier'));
      await tester.pumpAndSettle();
      await _typePin(tester, '3333');
      await tester.tap(find.widgetWithText(FilledButton, 'Unlock'));
      await tester.pumpAndSettle();

      expect(repository.lastUnlockMembershipId, 'm1');
      expect(repository.lastUnlockPin, '3333');
      expect(unlocked, isTrue);
    });

    testWidgets('a wrong PIN shows how many tries are left and stays locked', (
      tester,
    ) async {
      final repository = FakeAuthRepository(
        people: const [_carlo],
        unlockFailure: const UnauthorizedFailure(
          'That PIN is not right. 4 tries left.',
        ),
      );
      var unlocked = false;
      await tester.pumpWidget(
        _wrap(
          UnlockScreen(onUnlocked: () => unlocked = true),
          repository: repository,
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Carlo Cashier'));
      await tester.pumpAndSettle();
      await _typePin(tester, '9999');
      await tester.tap(find.widgetWithText(FilledButton, 'Unlock'));
      await tester.pumpAndSettle();

      expect(find.text('That PIN is not right. 4 tries left.'), findsOneWidget);
      expect(unlocked, isFalse);
    });

    testWidgets('a device the server no longer knows offers to pair again', (
      tester,
    ) async {
      final repository = FakeAuthRepository(
        failureToThrow: const UnauthorizedFailure('Device not recognized.'),
      );
      await tester.pumpWidget(
        _wrap(UnlockScreen(onUnlocked: () {}), repository: repository),
      );
      await tester.pumpAndSettle();

      expect(find.text('This device cannot be unlocked right now'), findsOneWidget);
      expect(find.text('Pair this device again'), findsOneWidget);
    });

    testWidgets('says plainly when nobody is set up for this device', (
      tester,
    ) async {
      final repository = FakeAuthRepository();
      await tester.pumpWidget(
        _wrap(UnlockScreen(onUnlocked: () {}), repository: repository),
      );
      await tester.pumpAndSettle();

      expect(find.textContaining('Nobody is set up'), findsOneWidget);
    });
  });

  group('PairDeviceScreen', () {
    testWidgets('submitting with an empty code shows an error and never pairs', (
      tester,
    ) async {
      final repository = FakeAuthRepository();
      await tester.pumpWidget(
        _wrap(PairDeviceScreen(onPaired: () {}), repository: repository),
      );

      await tester.tap(find.text('Pair device'));
      await tester.pump();

      expect(find.text('Required'), findsOneWidget);
      expect(repository.lastPairingCode, isNull);
    });

    testWidgets('a valid code pairs the device and reports it', (tester) async {
      final repository = FakeAuthRepository();
      var paired = false;
      await tester.pumpWidget(
        _wrap(
          PairDeviceScreen(onPaired: () => paired = true),
          repository: repository,
        ),
      );

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Pairing code'),
        'K7M2-9QXP',
      );
      await tester.tap(find.text('Pair device'));
      await tester.pumpAndSettle();

      expect(repository.lastPairingCode, 'K7M2-9QXP');
      expect(paired, isTrue);
    });

    testWidgets('a rejected code shows the failure and does not report paired', (
      tester,
    ) async {
      final repository = FakeAuthRepository(
        failureToThrow: const UnauthorizedFailure(
          'The code was not recognized, has already been used, or has expired.',
        ),
      );
      var paired = false;
      await tester.pumpWidget(
        _wrap(
          PairDeviceScreen(onPaired: () => paired = true),
          repository: repository,
        ),
      );

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Pairing code'),
        'WRONG',
      );
      await tester.tap(find.text('Pair device'));
      await tester.pumpAndSettle();

      expect(find.textContaining('already been used'), findsOneWidget);
      expect(paired, isFalse);
    });
  });
}
