import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';
import 'package:purch_client/features/onboarding/presentation/screens/department_list_screen.dart';

import '../../../helpers/fake_onboarding_repository.dart';

Widget _wrap(FakeOnboardingRepository repository) {
  return ProviderScope(
    overrides: [onboardingRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(
      home: DepartmentListScreen(branchId: 'branch-1', branchName: 'Main'),
    ),
  );
}

void main() {
  testWidgets('shows an empty state when there are no departments yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeOnboardingRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No departments yet — tap + to add one.'), findsOneWidget);
  });

  testWidgets('adding a department from the + button refreshes the list', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(
        TextFormField,
        'Department name (e.g. "Bakery Stall")',
      ),
      'Bakery Stall',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Add Department'));
    await tester.pumpAndSettle();

    expect(find.text('Bakery Stall'), findsOneWidget);
    expect(repository.departmentsByBranch['branch-1'], hasLength(1));
  });
}
