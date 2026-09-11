import 'payment_method.dart';

/// Mirrors Purch.Domain.Enums.TransactionStatus exactly, in declared order.
enum TransactionStatus { open, awaitingPayment, completed, voided, refunded }

/// Mirrors Purch.Application.Pos.TransactionDto — the cart engine's view of
/// a device's single in-progress sale.
class Transaction {
  const Transaction({
    required this.id,
    required this.branchId,
    required this.deviceId,
    required this.status,
    required this.lines,
    required this.subtotal,
    required this.discountAmount,
    required this.seniorPwdDiscountApplied,
    required this.totalAmount,
    required this.receiptNumber,
    required this.payments,
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
      seniorPwdDiscountApplied: json['seniorPwdDiscountApplied'] as bool,
      totalAmount: (json['totalAmount'] as num).toDouble(),
      receiptNumber: (json['receiptNumber'] as num?)?.toInt(),
      payments:
          (json['payments'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(Payment.fromJson)
              .toList(),
    );
  }

  final String id;
  final String branchId;
  final String deviceId;
  final TransactionStatus status;
  final List<TransactionLine> lines;
  final double subtotal;
  final double discountAmount;
  final bool seniorPwdDiscountApplied;
  final double totalAmount;
  final int? receiptNumber;
  final List<Payment> payments;

  int get itemCount =>
      lines.fold(0, (total, line) => total + line.quantity.ceil());
}

/// Mirrors Purch.Application.Pos.PaymentDto.
class Payment {
  const Payment({
    required this.id,
    required this.method,
    required this.status,
    required this.amount,
    required this.amountTendered,
    required this.changeGiven,
  });

  factory Payment.fromJson(Map<String, dynamic> json) {
    return Payment(
      id: json['id'] as String,
      method: PaymentMethod.values[json['method'] as int],
      status: PaymentStatus.values[json['status'] as int],
      amount: (json['amount'] as num).toDouble(),
      amountTendered: (json['amountTendered'] as num?)?.toDouble(),
      changeGiven: (json['changeGiven'] as num?)?.toDouble(),
    );
  }

  final String id;
  final PaymentMethod method;
  final PaymentStatus status;
  final double amount;
  final double? amountTendered;
  final double? changeGiven;
}

/// Mirrors Purch.Application.Pos.RecordPaymentRequest. Only cash,
/// bankTransfer, and manualGcashQr are accepted by the backend so far.
class RecordPaymentRequest {
  const RecordPaymentRequest({required this.method, this.amountTendered});

  final PaymentMethod method;
  final double? amountTendered;

  Map<String, dynamic> toJson() => {
    'method': method.index,
    'amountTendered': amountTendered,
  };
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
    required this.comboSelections,
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
      comboSelections:
          (json['comboSelections'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(TransactionLineComboSelection.fromJson)
              .toList(),
    );
  }

  final String id;
  final String itemId;
  final String itemName;
  final String? itemVariantId;
  final double quantity;
  final double unitPrice;
  final double lineTotal;
  final List<TransactionLineComboSelection> comboSelections;
}

/// Mirrors Purch.Application.Pos.ComboSelectionDto — a resolved slot/item pick
/// on a Combo line, shown on the cart/receipt so the cashier and customer can
/// see what was actually picked.
class TransactionLineComboSelection {
  const TransactionLineComboSelection({
    required this.slotId,
    required this.slotLabel,
    required this.selectedItemId,
    required this.selectedItemName,
  });

  factory TransactionLineComboSelection.fromJson(Map<String, dynamic> json) {
    return TransactionLineComboSelection(
      slotId: json['slotId'] as String,
      slotLabel: json['slotLabel'] as String,
      selectedItemId: json['selectedItemId'] as String,
      selectedItemName: json['selectedItemName'] as String,
    );
  }

  final String slotId;
  final String slotLabel;
  final String selectedItemId;
  final String selectedItemName;
}

/// Mirrors Purch.Application.Pos.AddTransactionLineRequest.
class AddTransactionLineRequest {
  const AddTransactionLineRequest({
    required this.itemId,
    this.itemVariantId,
    required this.quantity,
    this.comboSelections,
  });

  final String itemId;
  final String? itemVariantId;
  final double quantity;
  final List<ComboSelectionRequest>? comboSelections;

  Map<String, dynamic> toJson() => {
    'itemId': itemId,
    'itemVariantId': itemVariantId,
    'quantity': quantity,
    'comboSelections': comboSelections?.map((s) => s.toJson()).toList(),
  };
}

/// Mirrors Purch.Application.Pos.ComboSelectionRequest — one picked component
/// for a Combo item's slot. A slot requiring N items needs N of these
/// carrying the same slotId.
class ComboSelectionRequest {
  const ComboSelectionRequest({
    required this.slotId,
    required this.selectedItemId,
  });

  final String slotId;
  final String selectedItemId;

  Map<String, dynamic> toJson() => {
    'slotId': slotId,
    'selectedItemId': selectedItemId,
  };
}

/// Mirrors Purch.Application.Pos.UpdateTransactionLineRequest.
class UpdateTransactionLineRequest {
  const UpdateTransactionLineRequest({required this.quantity});

  final double quantity;

  Map<String, dynamic> toJson() => {'quantity': quantity};
}

/// Mirrors Purch.Application.Pos.ApplySeniorPwdDiscountRequest. The cashier
/// toggles this only after verifying the customer's physical Senior
/// Citizen/PWD ID themselves — this is not an ID-scanning feature.
class ApplySeniorPwdDiscountRequest {
  const ApplySeniorPwdDiscountRequest({required this.apply});

  final bool apply;

  Map<String, dynamic> toJson() => {'apply': apply};
}
