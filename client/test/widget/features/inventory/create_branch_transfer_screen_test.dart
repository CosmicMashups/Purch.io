import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/inventory/presentation/providers/branch_transfer_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/create_branch_transfer_screen.dart';
import 'package:purch_client/features/onboarding/domain/branch_models.dart';
import 'package:purch_client/features/onboarding/domain/hardware_enums.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';

import '../../../helpers/fake_branch_transfer_repository.dart';
import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_onboarding_repository.dart';

const _water = Item(
  id: 'item-1',
  name: 'Bottled Water',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 15,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 100,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _mainBranch = Branch(
  id: 'branch-1',
  name: 'Main Branch',
  address: null,
  receiptPrinterProfile: ReceiptPrinterProfile.none,
  cashDrawerEnabled: false,
  cashDrawerPolicy: CashDrawerPolicy.kickOnSaleOnly,
  manualGcashQrImageUrl: null,
  manualGcashAccountName: null,
  manualGcashAccountNumber: null,
);

const _secondBranch = Branch(
  id: 'branch-2',
  name: 'Branch 2',
  address: null,
  receiptPrinterProfile: ReceiptPrinterProfile.none,
  cashDrawerEnabled: false,
  cashDrawerPolicy: CashDrawerPolicy.kickOnSaleOnly,
  manualGcashQrImageUrl: null,
  manualGcashAccountName: null,
  manualGcashAccountNumber: null,
);

Widget _wrap({
  required FakeBranchTransferRepository transferRepository,
  required FakeCatalogRepository catalogRepository,
  required FakeOnboardingRepository onboardingRepository,
}) {
  return ProviderScope(
    overrides: [
      branchTransferRepositoryProvider.overrideWithValue(transferRepository),
      catalogRepositoryProvider.overrideWithValue(catalogRepository),
      onboardingRepositoryProvider.overrideWithValue(onboardingRepository),
    ],
    child: const MaterialApp(home: CreateBranchTransferScreen()),
  );
}

void main() {
  testWidgets('creating a transfer submits source, destination, and lines', (
    tester,
  ) async {
    final transferRepository = FakeBranchTransferRepository();
    final catalogRepository = FakeCatalogRepository(initialItems: [_water]);
    final onboardingRepository = FakeOnboardingRepository(
      initialBranches: [_mainBranch, _secondBranch],
    );
    await tester.pumpWidget(
      _wrap(
        transferRepository: transferRepository,
        catalogRepository: catalogRepository,
        onboardingRepository: onboardingRepository,
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Branch>, 'Source branch'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Main Branch').last);
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(
        DropdownButtonFormField<Branch>,
        'Destination branch',
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Branch 2').last);
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Item>, 'Item'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Bottled Water').last);
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextField, 'Qty'), '20');
    await tester.pump();

    await tester.tap(find.widgetWithText(FilledButton, 'Create Transfer'));
    await tester.pumpAndSettle();

    expect(transferRepository.lastCreateRequest?.sourceBranchId, 'branch-1');
    expect(
      transferRepository.lastCreateRequest?.destinationBranchId,
      'branch-2',
    );
    final line = transferRepository.lastCreateRequest!.lines.single;
    expect(line.itemId, 'item-1');
    expect(line.quantity, 20);
  });

  testWidgets('Add Item adds another item/quantity row', (tester) async {
    final transferRepository = FakeBranchTransferRepository();
    final catalogRepository = FakeCatalogRepository(initialItems: [_water]);
    final onboardingRepository = FakeOnboardingRepository(
      initialBranches: [_mainBranch, _secondBranch],
    );
    await tester.pumpWidget(
      _wrap(
        transferRepository: transferRepository,
        catalogRepository: catalogRepository,
        onboardingRepository: onboardingRepository,
      ),
    );
    await tester.pumpAndSettle();

    expect(
      find.widgetWithText(DropdownButtonFormField<Item>, 'Item'),
      findsOneWidget,
    );

    final addItemButton = find.text('Add Item');
    await tester.ensureVisible(addItemButton);
    await tester.tap(addItemButton);
    await tester.pumpAndSettle();

    expect(
      find.widgetWithText(DropdownButtonFormField<Item>, 'Item'),
      findsNWidgets(2),
    );
  });
}
