/// Mirrors Purch.Domain.Enums.TransactionStatus exactly, in declared order.
enum TransactionStatus { open, awaitingPayment, completed, voided, refunded }

/// Mirrors Purch.Application.Pos.TransactionDto — the cart engine's view of
/// a device's single in-progress sale. Discounts/payment/receipt-numbering
/// aren't modeled yet; those land with the rest of Phase 4's D5/D6 work.
class Transaction {
  const Transaction({
    required this.id,
    required this.branchId,
    required this.deviceId,
    required this.status,
    required this.lines,
    required this.subtotal,
    required this.discountAmount,
    required this.totalAmount,
  });

  factory Transaction.fromJson(Map<String, dynamic> json) {
    return Transaction(
      id: json['id'] as String,
      branchId: json['branchId'] as String,
      deviceId: json['deviceId'] as String,
      status: TransactionStatus.values[json['status'] as int],
      lines:
          (json['lines'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(TransactionLine.fromJson)
              .toList(),
      subtotal: (json['subtotal'] as num).toDouble(),
      discountAmount: (json['discountAmount'] as num).toDouble(),
      totalAmount: (json['totalAmount'] as num).toDouble(),
    );
  }

  final String id;
  final String branchId;
  final String deviceId;
  final TransactionStatus status;
  final List<TransactionLine> lines;
  final double subtotal;
  final double discountAmount;
  final double totalAmount;

  int get itemCount =>
      lines.fold(0, (total, line) => total + line.quantity.ceil());
}

/// Mirrors Purch.Application.Pos.TransactionLineDto.
class TransactionLine {
  const TransactionLine({
    required this.id,
    required this.itemId,
    required this.itemName,
    required this.itemVariantId,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });

  factory TransactionLine.fromJson(Map<String, dynamic> json) {
    return TransactionLine(
      id: json['id'] as String,
      itemId: json['itemId'] as String,
      itemName: json['itemName'] as String,
      itemVariantId: json['itemVariantId'] as String?,
      quantity: (json['quantity'] as num).toDouble(),
      unitPrice: (json['unitPrice'] as num).toDouble(),
      lineTotal: (json['lineTotal'] as num).toDouble(),
    );
  }

  final String id;
  final String itemId;
  final String itemName;
  final String? itemVariantId;
  final double quantity;
  final double unitPrice;
  final double lineTotal;
}

/// Mirrors Purch.Application.Pos.AddTransactionLineRequest. Only
/// PricingType.unit items are addable from the item grid so far — combo/
/// variant customization sheets (D2/D3) aren't built yet.
class AddTransactionLineRequest {
  const AddTransactionLineRequest({
    required this.itemId,
    this.itemVariantId,
    required this.quantity,
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

/// Mirrors Purch.Application.Pos.UpdateTransactionLineRequest.
class UpdateTransactionLineRequest {
  const UpdateTransactionLineRequest({required this.quantity});

  final double quantity;

  Map<String, dynamic> toJson() => {'quantity': quantity};
}
