import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/routing/auth_gate.dart';
import 'package:purch_client/core/sync/sync_providers.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/credit_ledger/presentation/providers/credit_ledger_providers.dart';
import 'package:purch_client/features/home/presentation/screens/home_tab_screen.dart';
import 'package:purch_client/features/inventory/domain/inventory_movement_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/inventory_providers.dart';
import 'package:purch_client/features/onboarding/domain/onboarding_enums.dart';
import 'package:purch_client/features/reports/domain/department_sales_models.dart';
import 'package:purch_client/features/reports/domain/inventory_report_models.dart';
import 'package:purch_client/features/reports/domain/sales_dashboard_models.dart';
import 'package:purch_client/features/reports/domain/staff_performance_models.dart';
import 'package:purch_client/features/reports/presentation/providers/reports_providers.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_credit_ledger_repository.dart';
import '../../../helpers/fake_inventory_repository.dart';
import '../../../helpers/fake_reports_repository.dart';
import '../../../helpers/fake_sync_repository.dart';

/// Replaces the four deleted reports-screen tests. The Reports tab is gone
/// and Home renders the same repository data as charts, so the coverage for
/// "the reporting data reaches the UI, and every surface has a real
/// empty/error state" lives here now.

const _rice = Item(
  id: 'item-1',
  name: 'Rice Meal',
  sku: null,
  barcode: null,
  categoryId: 'cat-1',
  basePrice: 85,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 10,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

SalesDashboard _dashboard() {
  final today = DateTime.now();
  final startOfToday = DateTime(today.year, today.month, today.day);
  return SalesDashboard(
    revenueToday: 500,
    revenueLast7Days: 2500,
    revenueLast30Days: 9000,
    trend: [
      for (var i = 13; i >= 0; i--)
        DailyRevenuePoint(
          date: startOfToday.subtract(Duration(days: i)),
          revenue: 100.0 + i,
        ),
    ],
    topSellingItems: const [
      TopSellingItem(
        itemId: 'item-1',
        itemName: 'Rice Meal',
        quantitySold: 10,
        revenue: 850,
      ),
    ],
    branchComparison: const [
      BranchRevenue(
        branchId: 'branch-1',
        branchName: 'Main Branch',
        revenue: 8000,
      ),
    ],
  );
}

Widget _wrap({
  FakeReportsRepository? reportsRepository,
  FakeInventoryRepository? inventoryRepository,
  FakeCatalogRepository? catalogRepository,
  StaffRole role = StaffRole.admin,
}) {
  return ProviderScope(
    overrides: [
      currentStaffRoleProvider.overrideWith((ref) async => role),
      reportsRepositoryProvider.overrideWithValue(
        reportsRepository ?? FakeReportsRepository(),
      ),
      inventoryRepositoryProvider.overrideWithValue(
        inventoryRepository ?? FakeInventoryRepository(),
      ),
      catalogRepositoryProvider.overrideWithValue(
        catalogRepository ?? FakeCatalogRepository(),
      ),
      syncRepositoryProvider.overrideWithValue(FakeSyncRepository()),
      creditLedgerRepositoryProvider.overrideWithValue(
        FakeCreditLedgerRepository(),
      ),
    ],
    child: const MaterialApp(home: HomeTabScreen()),
  );
}

/// Home is a long scrolling report. Widget tests get an 800x600 surface by
/// default, so the lower charts would never be built — give the test a
/// landscape-tablet-sized surface tall enough to lay the whole page out.
void _useTabletSurface(WidgetTester tester) {
  tester.view.physicalSize = const Size(1400, 3200);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

void main() {
  testWidgets('renders the sales trend with its range filter', (tester) async {
    _useTabletSurface(tester);
    await tester.pumpWidget(
      _wrap(
        reportsRepository: FakeReportsRepository(salesDashboard: _dashboard()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Sales Trend'), findsOneWidget);
    // Day / Week / Month / Year presets plus a calendar-picked custom range.
    expect(find.text('Day'), findsOneWidget);
    expect(find.text('Week'), findsOneWidget);
    expect(find.text('Month'), findsOneWidget);
    expect(find.text('Year'), findsOneWidget);
    expect(find.text('Custom'), findsOneWidget);
  });

  testWidgets('revenue cards carry a real comparison or say they cannot', (
    tester,
  ) async {
    _useTabletSurface(tester);
    await tester.pumpWidget(
      _wrap(
        reportsRepository: FakeReportsRepository(salesDashboard: _dashboard()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('TODAY'), findsOneWidget);
    expect(find.text('LAST 7 DAYS'), findsOneWidget);
    expect(find.text('LAST 30 DAYS'), findsOneWidget);
    expect(find.text('₱500.00'), findsWidgets);

    // The 14-day series covers a prior week but not a prior 30 days, so the
    // 30-day card must decline to invent a percentage.
    expect(find.text('No prior period to compare'), findsWidgets);
  });

  testWidgets('every chart shows a designed empty state, not a bare spinner', (
    tester,
  ) async {
    _useTabletSurface(tester);
    await tester.pumpWidget(_wrap());
    await tester.pumpAndSettle();

    expect(find.text('No sales recorded in this range.'), findsOneWidget);
    expect(find.text('No stock movements in this range.'), findsOneWidget);
    expect(find.text('Nothing is running low right now.'), findsOneWidget);
    expect(find.text('No staff sales in this range.'), findsOneWidget);
    expect(find.text('No shifts opened in this range.'), findsOneWidget);
    expect(
      find.text('No department revenue in this range.'),
      findsOneWidget,
    );
    expect(find.text('No category revenue yet.'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsNothing);
  });

  testWidgets('renders staff, movement, department and category data', (
    tester,
  ) async {
    _useTabletSurface(tester);
    final reportsRepository = FakeReportsRepository(
      salesDashboard: _dashboard(),
      movementSummary: MovementSummary(
        from: DateTime(2026, 1, 1),
        to: DateTime(2026, 1, 14),
        byType: const [
          MovementTypeSummary(
            type: MovementType.stockIn,
            totalQuantity: 40,
            movementCount: 4,
          ),
          MovementTypeSummary(
            type: MovementType.spoiled,
            totalQuantity: 3,
            movementCount: 1,
          ),
        ],
      ),
      staffPerformance: const StaffPerformanceReport(
        sales: [
          StaffSalesSummary(
            staffUserId: 'u-1',
            staffName: 'Maria Santos',
            transactionCount: 12,
            totalSales: 3400,
          ),
        ],
        shiftAttendance: [
          StaffShiftAttendance(
            staffUserId: 'u-1',
            staffName: 'Maria Santos',
            shiftsOpened: 5,
            shiftsWithVariance: 1,
          ),
        ],
      ),
      departmentSales: const [
        DepartmentSalesSummary(
          departmentId: 'd-1',
          departmentName: 'Grocery',
          revenue: 6000,
        ),
      ],
    );

    await tester.pumpWidget(
      _wrap(
        reportsRepository: reportsRepository,
        catalogRepository: FakeCatalogRepository(
          initialItems: [_rice],
          initialCategories: const [
            Category(id: 'cat-1', name: 'Meals', sortOrder: 0),
          ],
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Stock-In'), findsWidgets);
    expect(find.textContaining('Maria Santos leads with 12'), findsOneWidget);
    expect(find.text('Grocery'), findsOneWidget);
    expect(find.text('Meals'), findsOneWidget);
  });

  testWidgets('the sales trend header does not overflow on a phone', (
    tester,
  ) async {
    // Portrait phone: the ChartCard spans the full page width, so its header
    // can't fit the title beside the five-chip range filter. The filter has
    // to stack under the title rather than overflow the header row.
    tester.view.physicalSize = const Size(400, 5200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _wrap(
        reportsRepository: FakeReportsRepository(salesDashboard: _dashboard()),
      ),
    );
    await tester.pumpAndSettle();

    // A RenderFlex overflow surfaces here rather than only as red stripes.
    expect(tester.takeException(), isNull);
    expect(find.text('Sales Trend'), findsOneWidget);
    expect(find.text('Custom'), findsOneWidget);
  });

  testWidgets('cashiers get the quick actions but not the reporting charts', (
    tester,
  ) async {
    _useTabletSurface(tester);
    await tester.pumpWidget(_wrap(role: StaffRole.cashier));
    await tester.pumpAndSettle();

    expect(find.text('New Sale'), findsOneWidget);
    expect(find.text('Sales Trend'), findsNothing);
    expect(find.text('Sales per Cashier'), findsNothing);
  });
}
