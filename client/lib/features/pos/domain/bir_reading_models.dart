/// Mirrors Purch.Domain.Enums.BirReadingType exactly, in declared order.
enum BirReadingType { x, z }

/// Mirrors Purch.Application.Reporting.BirReadingDto — F2/FR26's best-effort
/// BIR X-reading (mid-shift, re-runnable) / Z-reading (end-of-day, advances
/// the reset counter) report for this device. See the backend's
/// docs/adr/0005 — every field here is best-effort pending real BIR
/// accreditation review, not a compliance guarantee.
class BirReading {
  const BirReading({
    required this.type,
    required this.deviceId,
    required this.machineIdentificationNumber,
    required this.generatedAt,
    required this.beginningReceiptNumber,
    required this.endingReceiptNumber,
    required this.transactionCount,
    required this.grossSales,
    required this.vatableSales,
    required this.vatAmount,
    required this.seniorPwdDiscountTotal,
    required this.promoDiscountTotal,
    required this.totalDiscounts,
    required this.netSales,
    required this.voidedCount,
    required this.voidedAmount,
    required this.oldGrandAccumulatedSales,
    required this.newGrandAccumulatedSales,
    required this.resetCounter,
  });

  factory BirReading.fromJson(Map<String, dynamic> json) {
    return BirReading(
      type: BirReadingType.values[json['type'] as int],
      deviceId: json['deviceId'] as String,
      machineIdentificationNumber:
          json['machineIdentificationNumber'] as String,
      generatedAt: DateTime.parse(json['generatedAt'] as String),
      beginningReceiptNumber: (json['beginningReceiptNumber'] as num?)?.toInt(),
      endingReceiptNumber: (json['endingReceiptNumber'] as num?)?.toInt(),
      transactionCount: json['transactionCount'] as int,
      grossSales: (json['grossSales'] as num).toDouble(),
      vatableSales: (json['vatableSales'] as num).toDouble(),
      vatAmount: (json['vatAmount'] as num).toDouble(),
      seniorPwdDiscountTotal:
          (json['seniorPwdDiscountTotal'] as num).toDouble(),
      promoDiscountTotal: (json['promoDiscountTotal'] as num).toDouble(),
      totalDiscounts: (json['totalDiscounts'] as num).toDouble(),
      netSales: (json['netSales'] as num).toDouble(),
      voidedCount: json['voidedCount'] as int,
      voidedAmount: (json['voidedAmount'] as num).toDouble(),
      oldGrandAccumulatedSales:
          (json['oldGrandAccumulatedSales'] as num).toDouble(),
      newGrandAccumulatedSales:
          (json['newGrandAccumulatedSales'] as num).toDouble(),
      resetCounter: json['resetCounter'] as int,
    );
  }

  final BirReadingType type;
  final String deviceId;
  final String machineIdentificationNumber;
  final DateTime generatedAt;
  final int? beginningReceiptNumber;
  final int? endingReceiptNumber;
  final int transactionCount;
  final double grossSales;
  final double vatableSales;
  final double vatAmount;
  final double seniorPwdDiscountTotal;
  final double promoDiscountTotal;
  final double totalDiscounts;
  final double netSales;
  final int voidedCount;
  final double voidedAmount;
  final double oldGrandAccumulatedSales;
  final double newGrandAccumulatedSales;
  final int resetCounter;
}
