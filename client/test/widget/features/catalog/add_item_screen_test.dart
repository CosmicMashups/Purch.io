import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/add_item_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: AddItemScreen()),
  );
}

void main() {
  final testCategories = [
    const Category(id: 'cat-1', name: 'Beverages', sortOrder: 1),
    const Category(id: 'cat-2', name: 'Snacks', sortOrder: 2),
  ];

  testWidgets('renders base fields and creates a regular Unit pricing item', (
    tester,
  ) async {
    final repository = FakeCatalogRepository(initialCategories: testCategories);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Add Item'), findsWidgets);
    expect(find.widgetWithText(TextFormField, 'Item name'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, 'Price'), findsOneWidget);
    expect(find.text('Unit'), findsOneWidget);

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Item name'),
      'Bottled Water 500ml',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Price'),
      '15.00',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'SKU (optional)'),
      'SKU-BW500',
    );

    await tester.ensureVisible(find.widgetWithText(FilledButton, 'Add Item'));
    await tester.tap(find.widgetWithText(FilledButton, 'Add Item'));
    await tester.pumpAndSettle();

    expect(repository.items, hasLength(1));
    final created = repository.items.first;
    expect(created.name, 'Bottled Water 500ml');
    expect(created.basePrice, 15.00);
    expect(created.sku, 'SKU-BW500');
    expect(created.pricingType, PricingType.unit);
  });

  testWidgets('switching to Service pricing renders duration and saves duration', (
    tester,
  ) async {
    final repository = FakeCatalogRepository(initialCategories: testCategories);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Item name'),
      'Haircut Special',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Price'),
      '250.00',
    );

    // Switch to Service
    await tester.tap(find.text('Unit'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Service').last);
    await tester.pumpAndSettle();

    expect(find.text('Service Appointment Duration'), findsOneWidget);
    expect(find.text('30 m'), findsOneWidget);
    expect(find.text('45 m'), findsOneWidget);

    // Tap the 45m chip
    await tester.tap(find.text('45 m'));
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.widgetWithText(FilledButton, 'Add Item'));
    await tester.tap(find.widgetWithText(FilledButton, 'Add Item'));
    await tester.pumpAndSettle();

    expect(repository.items, hasLength(1));
    final created = repository.items.first;
    expect(created.name, 'Haircut Special');
    expect(created.pricingType, PricingType.service);
    expect(created.serviceDurationMinutes, 45);
  });

  testWidgets(
    'switching to Weight/Volume renders tingi options and updates tingi config',
    (tester) async {
      final repository = FakeCatalogRepository(initialCategories: testCategories);
      await tester.pumpWidget(_wrap(repository));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Item name'),
        'Jasmine Rice Sack',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Price'),
        '2400.00',
      );

      // Switch to Weight/Volume
      await tester.tap(find.text('Unit'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Weight/Volume').last);
      await tester.pumpAndSettle();

      expect(find.text('Weight / Volume Configuration (Tingi)'), findsOneWidget);
      expect(find.text('Whole pack'), findsOneWidget);
      expect(find.text('Step increment'), findsOneWidget);

      // Select step increment
      await tester.tap(find.text('Step increment'));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Pack size (e.g. 50 for 50kg)'),
        '50',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Increment step (e.g. 0.5 or 1.0)'),
        '1.0',
      );

      await tester.ensureVisible(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.tap(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.pumpAndSettle();

      expect(repository.items, hasLength(1));
      final created = repository.items.first;
      expect(created.name, 'Jasmine Rice Sack');
      expect(created.pricingType, PricingType.weightVolume);
      expect(created.tingiMode, TingiMode.increment);
      expect(created.packagedSize, 50.0);
      expect(created.tingiIncrementStep, 1.0);
    },
  );

  testWidgets(
    'switching to Bundle renders promo rule inputs and creates bundle rule',
    (tester) async {
      final repository = FakeCatalogRepository(initialCategories: testCategories);
      await tester.pumpWidget(_wrap(repository));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Item name'),
        'Instant Noodles',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Price'),
        '15.00',
      );

      // Switch to Bundle
      await tester.tap(find.text('Unit'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Bundle').last);
      await tester.pumpAndSettle();

      expect(find.text('Initial Bundle Promotion Rule (Optional)'), findsOneWidget);

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Trigger Qty'),
        '3',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Bundle Price'),
        '40.00',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Rule description (optional)'),
        '3 for ₱40 Promo',
      );

      await tester.ensureVisible(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.tap(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.pumpAndSettle();

      expect(repository.items, hasLength(1));
      final created = repository.items.first;
      expect(created.name, 'Instant Noodles');
      expect(created.pricingType, PricingType.bundle);
      expect(repository.bundleRules, hasLength(1));
      expect(repository.bundleRules.first.triggerQuantity, 3);
      expect(repository.bundleRules.first.bundlePrice, 40.00);
      expect(repository.bundleRules.first.description, '3 for ₱40 Promo');
    },
  );

  testWidgets(
    'switching to Variant Matrix renders attribute inputs and creates variant',
    (tester) async {
      final repository = FakeCatalogRepository(initialCategories: testCategories);
      await tester.pumpWidget(_wrap(repository));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Item name'),
        'Cotton Shirt',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Price'),
        '300.00',
      );

      // Switch to Variant Matrix
      await tester.tap(find.text('Unit'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Variant Matrix').last);
      await tester.pumpAndSettle();

      expect(find.text('Initial Variant Configuration (Optional)'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Attribute (e.g. Size)'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Value (e.g. Medium)'), findsOneWidget);

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Variant SKU (opt)'),
        'SHIRT-REG-01',
      );

      await tester.ensureVisible(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.tap(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.pumpAndSettle();

      expect(repository.items, hasLength(1));
      final created = repository.items.first;
      expect(created.name, 'Cotton Shirt');
      expect(created.pricingType, PricingType.variantMatrix);
      expect(repository.variants, hasLength(1));
      expect(repository.variants.first.sku, 'SHIRT-REG-01');
      expect(repository.variants.first.attributes, {'Size': 'Regular'});
    },
  );

  testWidgets(
    'switching to Combo renders slot inputs and creates combo component',
    (tester) async {
      final repository = FakeCatalogRepository(initialCategories: testCategories);
      await tester.pumpWidget(_wrap(repository));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Item name'),
        'Lunch Meal Combo',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Price'),
        '180.00',
      );

      // Switch to Combo
      await tester.tap(find.text('Unit'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Combo').last);
      await tester.pumpAndSettle();

      expect(find.text('Initial Combo Slot (Optional)'), findsOneWidget);

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Slot label (e.g. Choose a Drink)'),
        'Choose a Drink',
      );

      // Select category for slot
      await tester.tap(find.text('Select category for slot...'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Beverages').last);
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.tap(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.pumpAndSettle();

      expect(repository.items, hasLength(1));
      final created = repository.items.first;
      expect(created.name, 'Lunch Meal Combo');
      expect(created.pricingType, PricingType.combo);
      expect(repository.comboComponents, hasLength(1));
      expect(repository.comboComponents.first.slotLabel, 'Choose a Drink');
      expect(repository.comboComponents.first.componentCategoryId, 'cat-1');
    },
  );
}
