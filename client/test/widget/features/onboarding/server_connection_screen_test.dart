import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/onboarding/presentation/screens/server_connection_screen.dart';

void main() {
  // Deliberately narrow, same judgment call as the barcode scanner screen's
  // own tests: ServerConnectionController's connection test hits a real Dio
  // instance against /health, which this harness can't mock without a
  // network stack — so only the client-side validation path is covered here.
  testWidgets('submitting with an empty address shows a validation error', (
    tester,
  ) async {
    await tester.pumpWidget(
      const ProviderScope(child: MaterialApp(home: ServerConnectionScreen())),
    );

    await tester.tap(find.text('Test & Save'));
    await tester.pump();

    expect(find.text('Required'), findsOneWidget);
  });
}
