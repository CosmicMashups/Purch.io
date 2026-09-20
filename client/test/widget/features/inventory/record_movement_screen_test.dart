import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/auth/presentation/providers/auth_providers.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/inventory/domain/inventory_movement_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/inventory_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/record_movement_screen.dart';
import 'package:purch_client/features/onboarding/domain/branch_models.dart';
import 'package:purch_client/features/onboarding/domain/hardware_enums.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';

import '../../../helpers/fake_token_storage.dart';
import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_inventory_repository.dart';
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
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _branch = Branch(
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

Widget _wrap(
  FakeCatalogRepository catalogRepository,
  FakeOnboardingRepository onboardingRepository,
  FakeInventoryRepository inventoryRepository, {
  FakeTokenStorage? tokenStorage,
}) {
  return ProviderScope(
    overrides: [
      secureTokenStorageProvider.overrideWithValue(
        tokenStorage ?? FakeTokenStorage(),
      ),
      catalogRepositoryProvider.overrideWithValue(catalogRepository),
      onboardingRepositoryProvider.overrideWithValue(onboardingRepository),
      inventoryRepositoryProvider.overrideWithValue(inventoryRepository),
    ],
    child: const MaterialApp(home: RecordMovementScreen()),
  );
}

void main() {
  testWidgets('recording a stock-in submits the selected item and branch', (
    tester,
  ) async {
    final catalogRepository = FakeCatalogRepository(initialItems: [_water]);
    final onboardingRepository = FakeOnboardingRepository(
      initialBranches: [_branch],
    );
    final inventoryRepository = FakeInventoryRepository();
    await tester.pumpWidget(
      _wrap(catalogRepository, onboardingRepository, inventoryRepository),
    );
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Item>, 'Item'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Bottled Water').last);
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Branch>, 'Branch'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Main Branch').last);
    await tester.pumpAndSettle();

    final quantityField = find.widgetWithText(TextFormField, 'Quantity');
    await tester.enterText(quantityField, '25');
    await tester.pump();

    await tester.tap(find.widgetWithText(FilledButton, 'Record Movement'));
    await tester.pumpAndSettle();

    expect(inventoryRepository.lastRecordRequest?.itemId, 'item-1');
    expect(inventoryRepository.lastRecordRequest?.branchId, 'branch-1');
    expect(inventoryRepository.lastRecordRequest?.type, MovementType.stockIn);
    expect(inventoryRepository.lastRecordRequest?.quantity, 25);
  });

  testWidgets('a branch-scoped account is offered only its own branch', (
    tester,
  ) async {
    const other = Branch(
      id: 'branch-2',
      name: 'Second Branch',
      address: null,
      receiptPrinterProfile: ReceiptPrinterProfile.none,
      cashDrawerEnabled: false,
      cashDrawerPolicy: CashDrawerPolicy.kickOnSaleOnly,
      manualGcashQrImageUrl: null,
      manualGcashAccountName: null,
      manualGcashAccountNumber: null,
    );
    String part(Object o) =>
        base64Url.encode(utf8.encode(jsonEncode(o))).replaceAll('=', '');
    final token =
        '${part({'alg': 'none'})}.${part({'scope_type': 'Branch', 'scope_id': 'branch-2'})}.sig';

    await tester.pumpWidget(
      _wrap(
        FakeCatalogRepository(initialItems: [_water]),
        FakeOnboardingRepository(initialBranches: [_branch, other]),
        FakeInventoryRepository(),
        tokenStorage: FakeTokenStorage(accessToken: token),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Branch>, 'Branch'),
    );
    await tester.pumpAndSettle();

    expect(find.text('Second Branch'), findsWidgets);
    expect(find.text('Main Branch'), findsNothing);
  });

  testWidgets('spoiled requires a reason category before submitting', (
    tester,
  ) async {
    final catalogRepository = FakeCatalogRepository(initialItems: [_water]);
    final onboardingRepository = FakeOnboardingRepository(
      initialBranches: [_branch],
    );
    final inventoryRepository = FakeInventoryRepository();
    await tester.pumpWidget(
      _wrap(catalogRepository, onboardingRepository, inventoryRepository),
    );
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Item>, 'Item'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Bottled Water').last);
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Branch>, 'Branch'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Main Branch').last);
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(
        DropdownButtonFormField<MovementType>,
        'Movement type',
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Spoiled').last);
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextFormField, 'Quantity'), '2');
    await tester.pump();

    await tester.tap(find.widgetWithText(FilledButton, 'Record Movement'));
    await tester.pumpAndSettle();

    expect(inventoryRepository.lastRecordRequest, isNull);
    expect(find.text('Required for Spoiled'), findsOneWidget);
  });
}
