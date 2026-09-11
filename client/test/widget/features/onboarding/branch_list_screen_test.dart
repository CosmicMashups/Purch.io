import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';
import 'package:purch_client/features/onboarding/presentation/screens/branch_list_screen.dart';

import '../../../helpers/fake_onboarding_repository.dart';

Widget _wrap(FakeOnboardingRepository repository) {
  return ProviderScope(
    overrides: [onboardingRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: BranchListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no branches yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeOnboardingRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No branches yet — tap + to add one.'), findsOneWidget);
  });

  testWidgets('adding a branch from the + button refreshes the list', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Branch name'),
      'North Branch',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Add Branch'));
    await tester.pumpAndSettle();

    expect(find.text('North Branch'), findsOneWidget);
  });
}
