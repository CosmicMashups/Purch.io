import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/kiosk/presentation/providers/kiosk_providers.dart';
import 'package:purch_client/features/kiosk/presentation/screens/kiosk_pairing_screen.dart';

import '../../../helpers/fake_kiosk_session_repository.dart';

Widget _wrap(Widget child, {required FakeKioskSessionRepository repository}) {
  return ProviderScope(
    overrides: [kioskSessionRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp(home: child),
  );
}

void main() {
  testWidgets(
    'submitting with an empty pairing code shows a validation error and never calls the repository',
    (tester) async {
      final repository = FakeKioskSessionRepository();
      await tester.pumpWidget(
        _wrap(KioskPairingScreen(onPaired: () {}), repository: repository),
      );

      await tester.tap(find.text('Pair This Kiosk'));
      await tester.pump();

      expect(find.text('Required'), findsOneWidget);
      expect(repository.lastPairingCode, isNull);
    },
  );

  testWidgets('a successful pairing calls onPaired', (tester) async {
    final repository = FakeKioskSessionRepository();
    var paired = false;

    await tester.pumpWidget(
      _wrap(
        KioskPairingScreen(onPaired: () => paired = true),
        repository: repository,
      ),
    );

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Device pairing code'),
      'KIOSK-1',
    );
    await tester.tap(find.text('Pair This Kiosk'));
    await tester.pumpAndSettle();

    expect(repository.lastPairingCode, 'KIOSK-1');
    expect(paired, isTrue);
  });

  testWidgets(
    'a rejected pairing shows the failure message and does not call onPaired',
    (tester) async {
      final repository = FakeKioskSessionRepository(
        failureToThrow: const UnauthorizedFailure(
          'The device pairing code was not recognized.',
        ),
      );
      var paired = false;

      await tester.pumpWidget(
        _wrap(
          KioskPairingScreen(onPaired: () => paired = true),
          repository: repository,
        ),
      );

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Device pairing code'),
        'BAD-CODE',
      );
      await tester.tap(find.text('Pair This Kiosk'));
      await tester.pumpAndSettle();

      expect(
        find.text('The device pairing code was not recognized.'),
        findsOneWidget,
      );
      expect(paired, isFalse);
    },
  );
}
