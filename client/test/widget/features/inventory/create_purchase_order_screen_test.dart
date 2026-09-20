import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/auth/presentation/providers/auth_providers.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/inventory/domain/supplier_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/purchase_order_providers.dart';
import 'package:purch_client/features/inventory/presentation/providers/supplier_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/create_purchase_order_screen.dart';
import 'package:purch_client/features/onboarding/domain/branch_models.dart';
import 'package:purch_client/features/onboarding/domain/hardware_enums.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';

import '../../../helpers/fake_token_storage.dart';
import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_onboarding_repository.dart';
import '../../../helpers/fake_purchase_order_repository.dart';
import '../../../helpers/fake_supplier_repository.dart';

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

const _supplier = Supplier(
  id: 'supplier-1',
  name: 'Acme Distribution',
  contactInfo: null,
  isActive: true,
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

Widget _wrap({
  required FakePurchaseOrderRepository purchaseOrderRepository,
  required FakeSupplierRepository supplierRepository,
  required FakeCatalogRepository catalogRepository,
  required FakeOnboardingRepository onboardingRepository,
}) {
  return ProviderScope(
    overrides: [
      secureTokenStorageProvider.overrideWithValue(FakeTokenStorage()),
      purchaseOrderRepositoryProvider.overrideWithValue(
        purchaseOrderRepository,
      ),
      supplierRepositoryProvider.overrideWithValue(supplierRepository),
      catalogRepositoryProvider.overrideWithValue(catalogRepository),
      onboardingRepositoryProvider.overrideWithValue(onboardingRepository),
    ],
    child: const MaterialApp(home: CreatePurchaseOrderScreen()),
  );
}

void main() {
  testWidgets('creating a purchase order submits supplier, branch, and lines', (
    tester,
  ) async {
    final purchaseOrderRepository = FakePurchaseOrderRepository();
    final supplierRepository = FakeSupplierRepository(
      initialSuppliers: [_supplier],
    );
    final catalogRepository = FakeCatalogRepository(initialItems: [_water]);
    final onboardingRepository = FakeOnboardingRepository(
      initialBranches: [_branch],
    );
    await tester.pumpWidget(
      _wrap(
        purchaseOrderRepository: purchaseOrderRepository,
        supplierRepository: supplierRepository,
        catalogRepository: catalogRepository,
        onboardingRepository: onboardingRepository,
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Supplier>, 'Supplier'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Acme Distribution').last);
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Branch>, 'Deliver to branch'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Main Branch').last);
    await tester.pumpAndSettle();

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<Item>, 'Item'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Bottled Water').last);
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextField, 'Qty'), '100');
    await tester.enterText(
      find.widgetWithText(TextField, 'Expected unit cost'),
      '10',
    );
    await tester.pump();

    final submitButton = find.widgetWithText(
      FilledButton,
      'Create Purchase Order',
    );
    await tester.ensureVisible(submitButton);
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    expect(purchaseOrderRepository.lastCreateRequest?.supplierId, 'supplier-1');
    expect(purchaseOrderRepository.lastCreateRequest?.branchId, 'branch-1');
    final line = purchaseOrderRepository.lastCreateRequest!.lines.single;
    expect(line.itemId, 'item-1');
    expect(line.quantityOrdered, 100);
    expect(line.expectedUnitCost, 10);
  });
}
