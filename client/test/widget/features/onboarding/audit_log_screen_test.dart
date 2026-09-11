import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/onboarding/domain/audit_log_models.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';
import 'package:purch_client/features/onboarding/presentation/screens/audit_log_screen.dart';

import '../../../helpers/fake_onboarding_repository.dart';

Widget _wrap(FakeOnboardingRepository repository) {
  return ProviderScope(
    overrides: [onboardingRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: AuditLogScreen()),
  );
}

void main() {
  testWidgets(
    'shows an empty state before any sensitive action has been recorded',
    (tester) async {
      await tester.pumpWidget(_wrap(FakeOnboardingRepository()));
      await tester.pumpAndSettle();

      expect(
        find.text(
          'No sensitive actions recorded yet — this fills in as staff work.',
        ),
        findsOneWidget,
      );
    },
  );

  testWidgets('lists existing audit log entries with a readable action label', (
    tester,
  ) async {
    final repository = FakeOnboardingRepository(
      initialAuditLogs: [
        AuditLogEntry(
          id: 'log-1',
          actorUserId: 'user-1',
          actionType: AuditActionType.void_,
          targetEntityType: 'Transaction',
          targetEntityId: 'txn-1',
          createdAt: DateTime(2026, 1, 1),
        ),
      ],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Void'), findsOneWidget);
  });
}
