import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/assign_department_screen.dart';
import 'package:purch_client/features/onboarding/domain/branch_models.dart';
import 'package:purch_client/features/onboarding/domain/department_models.dart';
import 'package:purch_client/features/onboarding/domain/hardware_enums.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_onboarding_repository.dart';

const _mangoesItem = Item(
  id: 'item-1',
  name: 'Mangoes',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 120,
  imageUrl: null,
  pricingType: PricingType.weightVolume,
  stockOnHand: 50,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
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
) {
  return ProviderScope(
    overrides: [
      catalogRepositoryProvider.overrideWithValue(catalogRepository),
      onboardingRepositoryProvider.overrideWithValue(onboardingRepository),
    ],
    child: MaterialApp(home: AssignDepartmentScreen(item: _mangoesItem)),
  );
}

void main() {
  testWidgets('assigning a department succeeds and pops', (tester) async {
    final catalogRepository = FakeCatalogRepository(
      initialItems: [_mangoesItem],
    );
    final onboardingRepository = FakeOnboardingRepository(
      initialBranches: [_branch],
      initialDepartments: {
        'branch-1': [
          const Department(
            id: 'department-1',
            branchId: 'branch-1',
            name: 'Fruit Stand',
            concessionaireContactInfo: null,
          ),
        ],
      },
    );

    var popped = false;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          catalogRepositoryProvider.overrideWithValue(catalogRepository),
          onboardingRepositoryProvider.overrideWithValue(onboardingRepository),
        ],
        child: MaterialApp(
          navigatorObservers: [_PopObserver(onPop: () => popped = true)],
          home: AssignDepartmentScreen(item: _mangoesItem),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byType(DropdownButtonFormField<String?>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Fruit Stand').last);
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    expect(catalogRepository.items.single.departmentId, 'department-1');
    expect(popped, isTrue);
  });

  testWidgets('shows no departments as an empty dropdown without erroring', (
    tester,
  ) async {
    final catalogRepository = FakeCatalogRepository(
      initialItems: [_mangoesItem],
    );
    final onboardingRepository = FakeOnboardingRepository(
      initialBranches: [_branch],
    );

    await tester.pumpWidget(_wrap(catalogRepository, onboardingRepository));
    await tester.pumpAndSettle();

    expect(find.text('None'), findsOneWidget);
  });
}

class _PopObserver extends NavigatorObserver {
  _PopObserver({required this.onPop});

  final VoidCallback onPop;

  @override
  void didPop(Route<dynamic> route, Route<dynamic>? previousRoute) => onPop();
}
