import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/inventory/domain/branch_transfer_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/branch_transfer_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/branch_transfer_list_screen.dart';

import '../../../helpers/fake_branch_transfer_repository.dart';

const _pendingTransfer = BranchTransfer(
  id: 'transfer-1',
  sourceBranchId: 'branch-1',
  sourceBranchName: 'Main Branch',
  destinationBranchId: 'branch-2',
  destinationBranchName: 'Branch 2',
  status: BranchTransferStatus.pending,
  lines: [
    BranchTransferLine(
      id: 'line-1',
      itemId: 'item-1',
      itemName: 'Bottled Water',
      quantity: 20,
    ),
  ],
);

const _receivedTransfer = BranchTransfer(
  id: 'transfer-2',
  sourceBranchId: 'branch-1',
  sourceBranchName: 'Main Branch',
  destinationBranchId: 'branch-2',
  destinationBranchName: 'Branch 2',
  status: BranchTransferStatus.received,
  lines: [],
);

Widget _wrap(FakeBranchTransferRepository repository) {
  return ProviderScope(
    overrides: [branchTransferRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: BranchTransferListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no transfers', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeBranchTransferRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No transfers yet — tap + to start one.'), findsOneWidget);
  });

  testWidgets('a pending transfer shows a Mark In Transit action', (
    tester,
  ) async {
    final repository = FakeBranchTransferRepository(
      initialTransfers: [_pendingTransfer],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Main Branch → Branch 2'), findsOneWidget);
    expect(find.text('Pending'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Mark In Transit'));
    await tester.pumpAndSettle();

    expect(repository.transfers.single.status, BranchTransferStatus.inTransit);
  });

  testWidgets('a pending transfer can be cancelled', (tester) async {
    final repository = FakeBranchTransferRepository(
      initialTransfers: [_pendingTransfer],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
    await tester.pumpAndSettle();

    expect(repository.transfers.single.status, BranchTransferStatus.cancelled);
    expect(find.text('Cancelled'), findsOneWidget);
    // Nothing more can be done to a cancelled transfer.
    expect(find.byType(FilledButton), findsNothing);
    expect(find.widgetWithText(TextButton, 'Cancel'), findsNothing);
  });

  testWidgets('a received transfer offers neither action nor cancel', (
    tester,
  ) async {
    final repository = FakeBranchTransferRepository(
      initialTransfers: [_receivedTransfer],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(TextButton, 'Cancel'), findsNothing);
  });

  testWidgets('a received transfer shows no action button', (tester) async {
    final repository = FakeBranchTransferRepository(
      initialTransfers: [_receivedTransfer],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Received'), findsOneWidget);
    expect(find.byType(FilledButton), findsNothing);
  });
}
