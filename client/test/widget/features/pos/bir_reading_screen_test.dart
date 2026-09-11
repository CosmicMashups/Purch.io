import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/presentation/providers/bir_reading_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/bir_reading_screen.dart';

import '../../../helpers/fake_bir_reading_repository.dart';

Widget _wrap(FakeBirReadingRepository repository) {
  return ProviderScope(
    overrides: [birReadingRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: BirReadingScreen()),
  );
}

void main() {
  testWidgets('generating an X-reading shows the report without confirmation', (
    tester,
  ) async {
    final repository = FakeBirReadingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(OutlinedButton, 'X-Reading'));
    await tester.pumpAndSettle();

    expect(repository.xReadingCallCount, 1);
    expect(find.text('X-Reading'), findsWidgets);
    expect(find.text('₱100.00'), findsWidgets);
  });

  testWidgets('generating a Z-reading requires confirmation first', (
    tester,
  ) async {
    final repository = FakeBirReadingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, 'Z-Reading'));
    await tester.pumpAndSettle();

    expect(repository.zReadingCallCount, 0);
    expect(find.text('Generate Z-reading?'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Generate Z-Reading'));
    await tester.pumpAndSettle();

    expect(repository.zReadingCallCount, 1);
    expect(find.text('Reset counter'), findsOneWidget);
  });

  testWidgets('cancelling the Z-reading confirmation does not generate one', (
    tester,
  ) async {
    final repository = FakeBirReadingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, 'Z-Reading'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
    await tester.pumpAndSettle();

    expect(repository.zReadingCallCount, 0);
    expect(find.text('Reset counter'), findsNothing);
  });
}
