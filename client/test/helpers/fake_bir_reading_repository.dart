import 'package:purch_client/features/pos/domain/bir_reading_models.dart';
import 'package:purch_client/features/pos/domain/bir_reading_repository.dart';

class FakeBirReadingRepository implements BirReadingRepository {
  FakeBirReadingRepository({
    this.generateXReadingFailure,
    this.generateZReadingFailure,
  });

  final Object? generateXReadingFailure;
  final Object? generateZReadingFailure;

  int xReadingCallCount = 0;
  int zReadingCallCount = 0;
  int _resetCounter = 0;
  double _grandAccumulatedSales = 0;

  @override
  Future<BirReading> generateXReading() async {
    xReadingCallCount++;
    if (generateXReadingFailure != null) {
      throw generateXReadingFailure!;
    }
    return _reading(BirReadingType.x);
  }

  @override
  Future<BirReading> generateZReading() async {
    zReadingCallCount++;
    if (generateZReadingFailure != null) {
      throw generateZReadingFailure!;
    }
    final reading = _reading(BirReadingType.z);
    _resetCounter++;
    _grandAccumulatedSales += 100;
    return reading;
  }

  BirReading _reading(BirReadingType type) {
    return BirReading(
      type: type,
      deviceId: 'device-1',
      machineIdentificationNumber: 'PENDING-MIN-DEVICE12',
      generatedAt: DateTime(2026, 1, 1),
      beginningReceiptNumber: 1,
      endingReceiptNumber: 2,
      transactionCount: 2,
      grossSales: 100,
      vatableSales: 89.29,
      vatAmount: 10.71,
      seniorPwdDiscountTotal: 0,
      promoDiscountTotal: 0,
      totalDiscounts: 0,
      netSales: 100,
      voidedCount: 0,
      voidedAmount: 0,
      oldGrandAccumulatedSales: _grandAccumulatedSales,
      newGrandAccumulatedSales: _grandAccumulatedSales + 100,
      resetCounter: _resetCounter,
    );
  }
}
