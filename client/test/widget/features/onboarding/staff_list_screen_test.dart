import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
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

  testWidgets('lists existing staff with their duties', (tester) async {
    final repository = FakeOnboardingRepository(
      initialStaff: [
        const StaffMember(
          id: 's1',
          name: 'Ben Cashier',
          email: 'ben@example.com',
          role: MemberRole.staff,
          duties: StaffDuties.cashier,
          branchIds: ['branch-1'],
          isActive: true,
          hasPin: true,
        ),
      ],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Ben Cashier'), findsOneWidget);
    expect(find.text('Cashier · ben@example.com'), findsOneWidget);
  });

  testWidgets('inviting a person shows the single-use link once', (tester) async {
    final repository = FakeOnboardingRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Full name'),
      'New Hire',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Email'),
      'hire@example.com',
    );
    // Managers and Admins need no branch; a Staff member does, so pick Manager.
    await tester.tap(find.byType(DropdownButtonFormField<MemberRole>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Manager').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Make invitation link'));
    await tester.pumpAndSettle();

    expect(find.text('/enrol/invite-token-1'), findsOneWidget);
    expect(repository.lastCreateStaffRequest?.email, 'hire@example.com');
    expect(repository.lastCreateStaffRequest?.role, MemberRole.manager);
  });
}
