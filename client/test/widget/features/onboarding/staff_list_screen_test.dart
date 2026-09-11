import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/onboarding/domain/onboarding_enums.dart';
import 'package:purch_client/features/onboarding/domain/staff_models.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';
import 'package:purch_client/features/onboarding/presentation/screens/staff_list_screen.dart';

import '../../../helpers/fake_onboarding_repository.dart';

Widget _wrap(FakeOnboardingRepository repository) {
  return ProviderScope(
    overrides: [onboardingRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: StaffListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there is no staff yet', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(
      find.text('No staff yet — tap + to add your first one.'),
      findsOneWidget,
    );
  });

  testWidgets('lists existing staff with their role', (tester) async {
    final repository = FakeOnboardingRepository(
      initialStaff: [
        const StaffMember(
          id: 's1',
          name: 'Ben Cashier',
          role: StaffRole.cashier,
          scopeType: ScopeType.tenant,
          scopeId: null,
          branchId: null,
          isActive: true,
        ),
      ],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Ben Cashier'), findsOneWidget);
    expect(find.text('Cashier'), findsOneWidget);
  });

  testWidgets('adding a staff member from the + button refreshes the list', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Full name'),
      'New Hire',
    );
    await tester.enterText(find.widgetWithText(TextFormField, 'PIN'), '4321');
    await tester.tap(find.text('Add Staff Member'));
    await tester.pumpAndSettle();

    expect(find.text('New Hire'), findsOneWidget);
  });
}
