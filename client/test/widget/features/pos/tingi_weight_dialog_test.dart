import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:purch_client/core/hardware/hardware_providers.dart';
import 'package:purch_client/core/hardware/scale/scale_driver.dart';
import 'package:purch_client/core/hardware/scale/scale_service.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/pos/domain/pos_repository.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/tingi_weight_dialog.dart';

class _MockPosRepository extends Mock implements PosRepository {}

void main() {
  setUpAll(() {
    registerFallbackValue(
      const AddTransactionLineRequest(itemId: 'fallback', quantity: 1),
    );
  });

  const sampleWeighedItem = Item(
    id: 'item-tilapia',
    name: 'Fresh Tilapia',
    sku: 'TILAPIA-01',
    barcode: null,
    categoryId: 'cat-fresh',
    basePrice: 180.0,
    imageUrl: null,
    pricingType: PricingType.weightVolume,
    stockOnHand: 50.0,
    isActive: true,
    tingiMode: TingiMode.none,
    packagedSize: null,
    tingiIncrementStep: null,
    tingiAllowedSizes: [0.5, 1.0, 1.5],
    serviceDurationMinutes: null,
    departmentId: null,
    lowStockThreshold: 5.0,
  );

  const sampleTx = Transaction(
    id: 'tx-1',
    branchId: 'b-1',
    deviceId: 'd-1',
    status: TransactionStatus.open,
    lines: [],
    subtotal: 0.0,
    discountAmount: 0.0,
    seniorPwdDiscountApplied: false,
    promoCode: null,
    promoDiscountAmount: 0.0,
    totalAmount: 0.0,
    receiptNumber: null,
    payments: [],
  );

  testWidgets('renders live digital scale readout, stability badge, and calculated price',
      (tester) async {
    final mockRepo = _MockPosRepository();
    when(() => mockRepo.getOrCreateOpenCart()).thenAnswer((_) async => sampleTx);
    when(() => mockRepo.addLine(any())).thenAnswer((_) async => sampleTx);

    final mockScale = MockScaleDriver(initialWeight: 1.500, autoEmit: false);
    mockScale.setSimulatedWeight(1.500, isStable: true);
    final scaleService = ScaleService(driver: mockScale);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          posRepositoryProvider.overrideWithValue(mockRepo),
          scaleServiceProvider.overrideWithValue(scaleService),
        ],
        child: const MaterialApp(
          home: Scaffold(
            body: TingiWeightDialog(item: sampleWeighedItem),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Verify header
    expect(find.text('Fresh Tilapia'), findsOneWidget);
    expect(find.text('₱180.00 / kg'), findsOneWidget);

    // Verify digital readout & stability
    expect(find.text('1.500'), findsOneWidget);
    expect(find.text('STABLE'), findsOneWidget);

    // Line total: 1.5 * 180 = 270.00
    expect(find.text('₱270.00'), findsOneWidget);

    // Verify Add to Cart CTA
    final ctaFinder = find.widgetWithText(FilledButton, 'Add 1.500 kg to Cart');
    expect(ctaFinder, findsOneWidget);

    // Tap Add to Cart
    await tester.ensureVisible(ctaFinder);
    await tester.tap(ctaFinder);
    await tester.pumpAndSettle();

    verify(() => mockRepo.addLine(any(
          that: isA<AddTransactionLineRequest>().having(
            (r) => r.quantity,
            'quantity',
            equals(1.5),
          ),
        ))).called(1);
  });
}
