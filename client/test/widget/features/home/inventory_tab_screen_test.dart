import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/home/presentation/screens/inventory_tab_screen.dart';
import 'package:purch_client/features/inventory/domain/inventory_movement_models.dart';
import 'package:purch_client/features/inventory/domain/purchase_order_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/inventory_providers.dart';
import 'package:purch_client/features/inventory/presentation/providers/purchase_order_providers.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_inventory_repository.dart';
import '../../../helpers/fake_onboarding_repository.dart';
import '../../../helpers/fake_purchase_order_repository.dart';

/// Replaces inventory_dashboard_screen_test: the dashboard's stat cards and
/// low-stock list were folded into the Inventory tab, so their coverage moves
/// here, plus the new items table.

const _sugar = Item(
  id: 'item-1',
  name: 'Sugar',
  sku: null,
  barcode: null,
  categoryId: 'cat-1',
  basePrice: 20,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 3,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: 5,
);

const _dashboardWithAlert = InventoryDashboard(
  totalSkus: 10,
  outOfStockCount: 1,
  lowStockCount: 1,
  lowStockItems: [
    LowStockItem(
      itemId: 'item-1',
      itemName: 'Sugar',
      stockOnHand: 3,
      lowStockThreshold: 5,
    ),
  ],
);

const _emptyDashboard = InventoryDashboard(
  totalSkus: 0,
  outOfStockCount: 0,
  lowStockCount: 0,
  lowStockItems: [],
);

/// One received PO (8 @ ₱12) and one partially received PO (2 @ ₱22) for the
/// same item, so the quantity-weighted average is
/// (8*12 + 2*22) / 10 = ₱14.00 — and not the ₱17.00 an unweighted mean would
/// produce.
final _purchaseOrders = [
  PurchaseOrder(
    id: 'po-1',
    supplierId: 's-1',
    supplierName: 'Supplier One',
    branchId: 'b-1',
    branchName: 'Main',
    status: PurchaseOrderStatus.received,
    sentAt: DateTime(2026, 1, 1),
    lines: const [
      PurchaseOrderLine(
        id: 'line-1',
        itemId: 'item-1',
        itemName: 'Sugar',
        quantityOrdered: 8,
        quantityReceived: 8,
        expectedUnitCost: 12,
      ),
    ],
  ),
  PurchaseOrder(
    id: 'po-2',
    supplierId: 's-1',
    supplierName: 'Supplier One',
    branchId: 'b-1',
    branchName: 'Main',
    status: PurchaseOrderStatus.partiallyReceived,
    sentAt: DateTime(2026, 1, 2),
    lines: const [
      PurchaseOrderLine(
        id: 'line-2',
        itemId: 'item-1',
        itemName: 'Sugar',
        quantityOrdered: 10,
        quantityReceived: 2,
        expectedUnitCost: 22,
      ),
    ],
  ),
  // A draft PO must NOT influence the average — nothing was received.
  PurchaseOrder(
    id: 'po-3',
    supplierId: 's-1',
    supplierName: 'Supplier One',
    branchId: 'b-1',
    branchName: 'Main',
    status: PurchaseOrderStatus.draft,
    sentAt: null,
    lines: const [
      PurchaseOrderLine(
        id: 'line-3',
        itemId: 'item-1',
        itemName: 'Sugar',
        quantityOrdered: 100,
        quantityReceived: 0,
        expectedUnitCost: 999,
      ),
    ],
  ),
];

Widget _wrap({
  required FakeInventoryRepository inventoryRepository,
  FakeCatalogRepository? catalogRepository,
  List<PurchaseOrder>? purchaseOrders,
}) {
  return ProviderScope(
    overrides: [
      inventoryRepositoryProvider.overrideWithValue(inventoryRepository),
      catalogRepositoryProvider.overrideWithValue(
        catalogRepository ?? FakeCatalogRepository(),
      ),
      purchaseOrderRepositoryProvider.overrideWithValue(
        FakePurchaseOrderRepository(
          initialPurchaseOrders: purchaseOrders ?? [],
        ),
      ),
      onboardingRepositoryProvider.overrideWithValue(
        FakeOnboardingRepository(),
      ),
    ],
    child: const MaterialApp(home: InventoryTabScreen()),
  );
}

void main() {
  testWidgets('shows the stat cards and an empty low-stock state', (
    tester,
  ) async {
    await tester.pumpWidget(
      _wrap(
        inventoryRepository: FakeInventoryRepository(
          initialDashboard: _emptyDashboard,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Total SKUs'), findsOneWidget);
    expect(find.text('0'), findsWidgets);
    expect(find.text('Nothing is running low right now.'), findsOneWidget);
  });

  testWidgets('lists a low-stock alert with a reorder shortcut', (
    tester,
  ) async {
    await tester.pumpWidget(
      _wrap(
        inventoryRepository: FakeInventoryRepository(
          initialDashboard: _dashboardWithAlert,
        ),
        catalogRepository: FakeCatalogRepository(initialItems: [_sugar]),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Reorder'), findsOneWidget);

    await tester.tap(find.text('Reorder'));
    await tester.pumpAndSettle();

    expect(find.text('Record Movement'), findsWidgets);
  });

  testWidgets(
    'items table shows name, category, stock and a quantity-weighted '
    'average cost',
    (tester) async {
      await tester.pumpWidget(
        _wrap(
          inventoryRepository: FakeInventoryRepository(
            initialDashboard: _emptyDashboard,
          ),
          catalogRepository: FakeCatalogRepository(
            initialItems: [_sugar],
            initialCategories: const [
              Category(id: 'cat-1', name: 'Pantry', sortOrder: 0),
            ],
          ),
          purchaseOrders: _purchaseOrders,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Item'.toUpperCase()), findsOneWidget);
      expect(find.text('Avg. cost'.toUpperCase()), findsOneWidget);
      expect(find.text('Pantry'), findsOneWidget);
      expect(find.text('3'), findsWidgets);
      expect(find.text('₱14.00'), findsOneWidget);
    },
  );

  testWidgets('items with no received purchase orders show no average cost', (
    tester,
  ) async {
    await tester.pumpWidget(
      _wrap(
        inventoryRepository: FakeInventoryRepository(
          initialDashboard: _emptyDashboard,
        ),
        catalogRepository: FakeCatalogRepository(initialItems: [_sugar]),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('— no receipts'), findsOneWidget);
    expect(find.text('Uncategorized'), findsOneWidget);
  });

  testWidgets('catalog/stock/purchasing stay reachable as secondary nav', (
    tester,
  ) async {
    await tester.pumpWidget(
      _wrap(
        inventoryRepository: FakeInventoryRepository(
          initialDashboard: _emptyDashboard,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Manage Items'), findsOneWidget);
    expect(find.text('Stock Movements'), findsOneWidget);
    expect(find.text('Purchase Orders'), findsOneWidget);
  });
}
