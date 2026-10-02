import 'payment_method.dart';

/// Mirrors Purch.Application.Pos.CreateExchangeRequest. Always needs a manager/admin's approval.
/// [settlementMethod] is only required (and only sent) when the return and replacement totals differ.
class ExchangeRequest {
  const ExchangeRequest({
    required this.returnLines,
    required this.replacementLines,
    required this.reason,
    required this.approverPin,
    this.settlementMethod,
    this.settlementAmountTendered,
  });

  final List<ExchangeReturnLine> returnLines;
  final List<ExchangeReplacementLine> replacementLines;
  final String reason;
  final String approverPin;
  final PaymentMethod? settlementMethod;
  final double? settlementAmountTendered;

  Map<String, dynamic> toJson() => {
    'returnLines': [for (final line in returnLines) line.toJson()],
    'replacementLines': [for (final line in replacementLines) line.toJson()],
    'reason': reason,
    'approverPin': approverPin,
    if (settlementMethod != null) 'settlementMethod': settlementMethod!.index,
    if (settlementAmountTendered != null)
      'settlementAmountTendered': settlementAmountTendered,
  };
}

/// One existing line to return some or all of, at the price it was originally sold for.
class ExchangeReturnLine {
  const ExchangeReturnLine({
    required this.originalLineId,
    required this.quantity,
  });

  final String originalLineId;
  final double quantity;

  Map<String, dynamic> toJson() => {
    'originalLineId': originalLineId,
    'quantity': quantity,
  };
}

/// One item to take instead, priced by the server at today's price.
class ExchangeReplacementLine {
  const ExchangeReplacementLine({
    required this.itemId,
    required this.quantity,
    this.itemVariantId,
  });

  final String itemId;
  final String? itemVariantId;
  final double quantity;

  Map<String, dynamic> toJson() => {
    'itemId': itemId,
    'itemVariantId': itemVariantId,
    'quantity': quantity,
  };
}

/// Mirrors Purch.Application.Pos.AdjustmentLineDto.
class AdjustmentLine {
  const AdjustmentLine({
    required this.itemName,
    required this.quantity,
    required this.lineTotal,
  });

  factory AdjustmentLine.fromJson(Map<String, dynamic> json) => AdjustmentLine(
    itemName: json['itemName'] as String,
    quantity: (json['quantity'] as num).toDouble(),
    lineTotal: (json['lineTotal'] as num).toDouble(),
  );

  final String itemName;
  final double quantity;
  final double lineTotal;
}

/// Mirrors Purch.Application.Pos.AdjustmentDto — a recorded exchange.
class Adjustment {
  const Adjustment({
    required this.originalReceiptNumber,
    required this.approvedByName,
    required this.returnLines,
    required this.replacementLines,
    required this.priceDifference,
    required this.changeGiven,
  });

  factory Adjustment.fromJson(Map<String, dynamic> json) => Adjustment(
    originalReceiptNumber: (json['originalReceiptNumber'] as num?)?.toInt(),
    approvedByName: json['approvedByName'] as String,
    returnLines: [
      for (final line in json['returnLines'] as List<dynamic>)
        AdjustmentLine.fromJson(line as Map<String, dynamic>),
    ],
    replacementLines: [
      for (final line in json['replacementLines'] as List<dynamic>)
        AdjustmentLine.fromJson(line as Map<String, dynamic>),
    ],
    priceDifference: (json['priceDifference'] as num).toDouble(),
    changeGiven: (json['changeGiven'] as num?)?.toDouble(),
  );

  final int? originalReceiptNumber;
  final String approvedByName;
  final List<AdjustmentLine> returnLines;
  final List<AdjustmentLine> replacementLines;

  /// Replacement minus returned: positive means the customer owed more, negative means they were refunded.
  final double priceDifference;
  final double? changeGiven;
}
