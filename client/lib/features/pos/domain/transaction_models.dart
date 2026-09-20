import 'payment_method.dart';
import 'pricing_engine.dart' show PromoCodeNotApplied;

/// Mirrors Purch.Domain.Enums.TransactionStatus exactly, in declared order.
enum TransactionStatus { open, awaitingPayment, completed, voided, refunded }

/// Mirrors Purch.Domain.Enums.KitchenStatus exactly, in declared order.
enum KitchenStatus { queued, preparing, ready, pickedUp }

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
    required this.promoCode,
    required this.promoDiscountAmount,
    this.itemPromoDiscountAmount = 0,
    required this.totalAmount,
    required this.receiptNumber,
    this.orderType,
    this.originatedFromKiosk = false,
    this.kioskPrepNumber,
    this.kitchenStatus = KitchenStatus.queued,
    required this.payments,
    this.seniorPwdSavings,
    this.promoSavings,
    this.promoCodeNotApplied = PromoCodeNotApplied.none,
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
      promoCode: json['promoCode'] as String?,
      promoDiscountAmount: (json['promoDiscountAmount'] as num).toDouble(),
      itemPromoDiscountAmount:
          (json['itemPromoDiscountAmount'] as num?)?.toDouble() ?? 0,
      totalAmount: (json['totalAmount'] as num).toDouble(),
      receiptNumber: (json['receiptNumber'] as num?)?.toInt(),
      orderType: json['orderType'] as String?,
      originatedFromKiosk: json['originatedFromKiosk'] as bool,
      kioskPrepNumber: (json['kioskPrepNumber'] as num?)?.toInt(),
      kitchenStatus: KitchenStatus.values[json['kitchenStatus'] as int? ?? 0],
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
  final String? promoCode;
  final double promoDiscountAmount;
  final double itemPromoDiscountAmount;
  final double totalAmount;
  final int? receiptNumber;
  final String? orderType;
  final bool originatedFromKiosk;
  final int? kioskPrepNumber;
  final KitchenStatus kitchenStatus;
  final List<Payment> payments;

  /// What the Senior/PWD discount would take off this cart, and what the best
  /// promotion would — so the cashier can see both and let the customer pick
  /// the better deal (the two never combine). Only known for carts priced on
  /// this device; null for a cart that came from the server.
  final double? seniorPwdSavings;
  final double? promoSavings;

  /// Why a valid promo code on the cart is not discounting right now.
  final PromoCodeNotApplied promoCodeNotApplied;

  int get itemCount =>
      lines.fold(0, (total, line) => total + line.quantity.ceil());

  /// True for a sale completed at the counter while the terminal was offline:
  /// it is real to the customer (receipt, payment) but the server has not
  /// recorded it yet. Its id is local, not a server transaction id.
  bool get savedOffline => id.startsWith(offlineSaleIdPrefix);

  static const offlineSaleIdPrefix = 'offline-';
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
  const RecordPaymentRequest({
    required this.method,
    this.amountTendered,
    this.customerCreditLedgerId,
  });

  factory RecordPaymentRequest.fromJson(Map<String, dynamic> json) {
    return RecordPaymentRequest(
      method: PaymentMethod.values[json['method'] as int],
      amountTendered: (json['amountTendered'] as num?)?.toDouble(),
      customerCreditLedgerId: json['customerCreditLedgerId'] as String?,
    );
  }

  final PaymentMethod method;
  final double? amountTendered;
  final String? customerCreditLedgerId;

  Map<String, dynamic> toJson() => {
    'method': method.index,
    'amountTendered': amountTendered,
    'customerCreditLedgerId': customerCreditLedgerId,
  };
}

/// Mirrors Purch.Application.Pos.TransactionLineDto.
class TransactionLine {
  const TransactionLine({
    required this.id,
    required this.itemId,
    required this.itemName,
    required this.itemVariantId,
    this.itemVariantAttributes = const {},
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
    this.promoDiscountAmount = 0,
    this.appliedPromoLabel,
    required this.comboSelections,
    this.modifierSelections = const [],
  });

  factory TransactionLine.fromJson(Map<String, dynamic> json) {
    return TransactionLine(
      id: json['id'] as String,
      itemId: json['itemId'] as String,
      itemName: json['itemName'] as String,
      itemVariantId: json['itemVariantId'] as String?,
      itemVariantAttributes:
          (json['itemVariantAttributes'] as Map<String, dynamic>?)
              ?.cast<String, String>() ??
          const {},
      quantity: (json['quantity'] as num).toDouble(),
      unitPrice: (json['unitPrice'] as num).toDouble(),
      lineTotal: (json['lineTotal'] as num).toDouble(),
      promoDiscountAmount:
          (json['promoDiscountAmount'] as num?)?.toDouble() ?? 0,
      appliedPromoLabel: json['appliedPromoLabel'] as String?,
      comboSelections:
          (json['comboSelections'] as List<dynamic>?)
              ?.cast<Map<String, dynamic>>()
              .map(TransactionLineComboSelection.fromJson)
              .toList() ??
          const [],
      modifierSelections:
          (json['modifierSelections'] as List<dynamic>?)
              ?.cast<Map<String, dynamic>>()
              .map(TransactionLineModifierSelection.fromJson)
              .toList() ??
          const [],
    );
  }

  final String id;
  final String itemId;
  final String itemName;
  final String? itemVariantId;
  final Map<String, String> itemVariantAttributes;
  final double quantity;
  final double unitPrice;
  final double lineTotal;
  final double promoDiscountAmount;
  final String? appliedPromoLabel;
  final List<TransactionLineComboSelection> comboSelections;
  final List<TransactionLineModifierSelection> modifierSelections;

  /// e.g. "Size: Large, Color: Red" — mirrors ItemVariant.attributesLabel's
  /// join format so a variant reads the same way everywhere it's shown.
  String get variantAttributesLabel =>
      itemVariantAttributes.entries.map((e) => '${e.key}: ${e.value}').join(', ');
}

/// Mirrors Purch.Application.Pos.ModifierSelectionDto — a resolved modifier
/// pick on a line for cart and receipt display.
class TransactionLineModifierSelection {
  const TransactionLineModifierSelection({
    required this.itemModifierId,
    required this.modifierName,
    required this.modifierGroupName,
    required this.priceDelta,
  });

  factory TransactionLineModifierSelection.fromJson(Map<String, dynamic> json) {
    return TransactionLineModifierSelection(
      itemModifierId: json['itemModifierId'] as String,
      modifierName: json['modifierName'] as String,
      modifierGroupName: json['modifierGroupName'] as String,
      priceDelta: (json['priceDelta'] as num).toDouble(),
    );
  }

  final String itemModifierId;
  final String modifierName;
  final String modifierGroupName;
  final double priceDelta;
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
    this.selectedModifierIds,
  });

  factory AddTransactionLineRequest.fromJson(Map<String, dynamic> json) {
    return AddTransactionLineRequest(
      itemId: json['itemId'] as String,
      itemVariantId: json['itemVariantId'] as String?,
      quantity: (json['quantity'] as num).toDouble(),
      comboSelections:
          (json['comboSelections'] as List<dynamic>?)
              ?.cast<Map<String, dynamic>>()
              .map(ComboSelectionRequest.fromJson)
              .toList(),
      selectedModifierIds:
          (json['selectedModifierIds'] as List<dynamic>?)?.cast<String>().toList(),
    );
  }

  final String itemId;
  final String? itemVariantId;
  final double quantity;
  final List<ComboSelectionRequest>? comboSelections;
  final List<String>? selectedModifierIds;

  Map<String, dynamic> toJson() => {
    'itemId': itemId,
    'itemVariantId': itemVariantId,
    'quantity': quantity,
    'comboSelections': comboSelections?.map((s) => s.toJson()).toList(),
    'selectedModifierIds': selectedModifierIds,
  };
}

/// Mirrors Purch.Application.Pos.CheckoutRequest — a whole sale in one call.
/// [saleId] is the idempotency key: resending the same id after a lost
/// response returns the already-completed sale instead of charging twice.
class CheckoutRequest {
  const CheckoutRequest({
    required this.saleId,
    required this.lines,
    required this.seniorPwdDiscountApplied,
    required this.promoCode,
    required this.orderType,
    required this.payment,
    this.expectedTotal,
    this.receiptNumber,
    this.offlineSale = false,
    this.soldAt,
    this.rungByStaffId,
  });

  factory CheckoutRequest.fromJson(Map<String, dynamic> json) {
    return CheckoutRequest(
      saleId: json['saleId'] as String,
      lines:
          (json['lines'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(AddTransactionLineRequest.fromJson)
              .toList(),
      seniorPwdDiscountApplied: json['seniorPwdDiscountApplied'] as bool,
      promoCode: json['promoCode'] as String?,
      orderType: json['orderType'] as String?,
      payment: RecordPaymentRequest.fromJson(
        json['payment'] as Map<String, dynamic>,
      ),
      expectedTotal: (json['expectedTotal'] as num?)?.toDouble(),
      receiptNumber: (json['receiptNumber'] as num?)?.toInt(),
      offlineSale: json['offlineSale'] as bool? ?? false,
      soldAt:
          json['soldAt'] == null ? null : DateTime.parse(json['soldAt'] as String),
      rungByStaffId: json['rungByStaffId'] as String?,
    );
  }

  final String saleId;
  final List<AddTransactionLineRequest> lines;
  final bool seniorPwdDiscountApplied;
  final String? promoCode;
  final String? orderType;
  final RecordPaymentRequest payment;

  /// What the device showed the customer; the server refuses to charge if it
  /// prices the cart differently.
  final double? expectedTotal;

  /// The number this terminal issued for the sale from its own per-device
  /// sequence. Null lets the server issue one.
  final int? receiptNumber;

  /// True when the sale was already completed at the counter while the terminal
  /// was offline; the server then records it at its own price rather than
  /// refusing it over a price difference.
  final bool offlineSale;

  /// When the sale really happened (only meaningful with [offlineSale]).
  final DateTime? soldAt;

  /// Who was signed in when an offline sale was rung up. It syncs later under whoever is signed in
  /// then; the server verifies this person in the tenant to credit the sale and judge the
  /// Senior/PWD discount. Only an id: the server never takes a role from the device.
  final String? rungByStaffId;

  Map<String, dynamic> toJson() => {
    'saleId': saleId,
    'lines': lines.map((l) => l.toJson()).toList(),
    'seniorPwdDiscountApplied': seniorPwdDiscountApplied,
    'promoCode': promoCode,
    'orderType': orderType,
    'payment': payment.toJson(),
    'expectedTotal': expectedTotal,
    'receiptNumber': receiptNumber,
    'offlineSale': offlineSale,
    'soldAt': soldAt?.toUtc().toIso8601String(),
    'rungByStaffId': rungByStaffId,
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

  factory ComboSelectionRequest.fromJson(Map<String, dynamic> json) {
    return ComboSelectionRequest(
      slotId: json['slotId'] as String,
      selectedItemId: json['selectedItemId'] as String,
    );
  }

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

/// Mirrors Purch.Application.Pos.ApplyPromoCodeRequest. A null/blank code
/// clears whatever promo is currently applied.
class ApplyPromoCodeRequest {
  const ApplyPromoCodeRequest({this.code});

  final String? code;

  Map<String, dynamic> toJson() => {'code': code};
}

/// Mirrors Purch.Application.Pos.SetOrderTypeRequest — E4's fulfillment
/// choice (e.g. "Dine In"/"Take Out"). Free-form, not a fixed enum.
class SetOrderTypeRequest {
  const SetOrderTypeRequest({required this.orderType});

  final String orderType;

  Map<String, dynamic> toJson() => {'orderType': orderType};
}

/// Mirrors Purch.Application.Pos.UpdateKitchenStatusRequest — Kitchen Display's
/// request to advance a kiosk order's kitchen-prep state.
class UpdateKitchenStatusRequest {
  const UpdateKitchenStatusRequest({required this.kitchenStatus});

  final KitchenStatus kitchenStatus;

  Map<String, dynamic> toJson() => {'kitchenStatus': kitchenStatus.index};
}
