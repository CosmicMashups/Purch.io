import '../domain/transaction_models.dart';

/// One line of the on-device draft cart: what the cashier picked
/// ([request], exactly what will be replayed to the server at checkout) plus
/// everything resolved at add-time for display and pricing.
class LocalCartLine {
  const LocalCartLine({
    required this.id,
    required this.request,
    required this.itemName,
    required this.unitPrice,
    this.variantAttributes = const {},
    this.modifiers = const [],
    this.comboSelections = const [],
  });

  factory LocalCartLine.fromJson(Map<String, dynamic> json) {
    final request = json['request'] as Map<String, dynamic>;
    return LocalCartLine(
      id: json['id'] as String,
      request: AddTransactionLineRequest(
        itemId: request['itemId'] as String,
        itemVariantId: request['itemVariantId'] as String?,
        quantity: (request['quantity'] as num).toDouble(),
        comboSelections:
            (request['comboSelections'] as List<dynamic>?)
                ?.cast<Map<String, dynamic>>()
                .map(
                  (s) => ComboSelectionRequest(
                    slotId: s['slotId'] as String,
                    selectedItemId: s['selectedItemId'] as String,
                  ),
                )
                .toList(),
        selectedModifierIds:
            (request['selectedModifierIds'] as List<dynamic>?)
                ?.cast<String>()
                .toList(),
      ),
      itemName: json['itemName'] as String,
      unitPrice: (json['unitPrice'] as num).toDouble(),
      variantAttributes:
          (json['variantAttributes'] as Map<String, dynamic>?)
              ?.cast<String, String>() ??
          const {},
      modifiers:
          (json['modifiers'] as List<dynamic>?)
              ?.cast<Map<String, dynamic>>()
              .map(TransactionLineModifierSelection.fromJson)
              .toList() ??
          const [],
      comboSelections:
          (json['comboSelections'] as List<dynamic>?)
              ?.cast<Map<String, dynamic>>()
              .map(TransactionLineComboSelection.fromJson)
              .toList() ??
          const [],
    );
  }

  final String id;
  final AddTransactionLineRequest request;
  final String itemName;

  /// Base/variant price plus modifier deltas / combo upcharges — the same
  /// "fold everything into UnitPrice" the server does.
  final double unitPrice;
  final Map<String, String> variantAttributes;
  final List<TransactionLineModifierSelection> modifiers;
  final List<TransactionLineComboSelection> comboSelections;

  double get quantity => request.quantity;

  bool get isMergeable =>
      (request.selectedModifierIds?.isEmpty ?? true) &&
      (request.comboSelections?.isEmpty ?? true);

  LocalCartLine withQuantity(double quantity) => LocalCartLine(
    id: id,
    request: AddTransactionLineRequest(
      itemId: request.itemId,
      itemVariantId: request.itemVariantId,
      quantity: quantity,
      comboSelections: request.comboSelections,
      selectedModifierIds: request.selectedModifierIds,
    ),
    itemName: itemName,
    unitPrice: unitPrice,
    variantAttributes: variantAttributes,
    modifiers: modifiers,
    comboSelections: comboSelections,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'request': request.toJson(),
    'itemName': itemName,
    'unitPrice': unitPrice,
    'variantAttributes': variantAttributes,
    'modifiers': [
      for (final m in modifiers)
        {
          'itemModifierId': m.itemModifierId,
          'modifierName': m.modifierName,
          'modifierGroupName': m.modifierGroupName,
          'priceDelta': m.priceDelta,
        },
    ],
    'comboSelections': [
      for (final s in comboSelections)
        {
          'slotId': s.slotId,
          'slotLabel': s.slotLabel,
          'selectedItemId': s.selectedItemId,
          'selectedItemName': s.selectedItemName,
        },
    ],
  };
}

/// The device's in-progress sale, persisted locally so a crash or restart
/// never loses it and nothing touches the server until checkout.
class LocalCart {
  const LocalCart({
    required this.id,
    this.lines = const [],
    this.seniorPwdApplied = false,
    this.promoCode,
    this.orderType,
    this.serverBacked = false,
    required this.saleId,
    this.receiptNumber,
    this.checkoutAttempted = false,
  });

  factory LocalCart.fromJson(Map<String, dynamic> json) => LocalCart(
    id: json['id'] as String,
    lines:
        (json['lines'] as List<dynamic>)
            .cast<Map<String, dynamic>>()
            .map(LocalCartLine.fromJson)
            .toList(),
    seniorPwdApplied: json['seniorPwdApplied'] as bool? ?? false,
    promoCode: json['promoCode'] as String?,
    orderType: json['orderType'] as String?,
    serverBacked: json['serverBacked'] as bool? ?? false,
    // Drafts saved before checkout became one call have no saleId; the
    // repository fills one in on load.
    saleId: json['saleId'] as String? ?? '',
    receiptNumber: (json['receiptNumber'] as num?)?.toInt(),
    checkoutAttempted: json['checkoutAttempted'] as bool? ?? false,
  );

  final String id;
  final List<LocalCartLine> lines;
  final bool seniorPwdApplied;
  final String? promoCode;
  final String? orderType;

  /// True once a kiosk order was claimed: that cart lives on the server, so
  /// every operation goes straight to it instead of this local draft.
  final bool serverBacked;

  /// Idempotency key for this sale's checkout, generated with the cart and kept
  /// until the sale completes or is voided. Every attempt to pay this cart
  /// sends the same id, so a retry after a lost response returns the sale that
  /// already went through instead of charging twice.
  final String saleId;

  /// The receipt number this device reserved for the sale at its first payment
  /// attempt, from the terminal's own sequence. Kept with the draft so every
  /// retry sends the same number — the receipt must carry one number only.
  final int? receiptNumber;

  /// Whether a checkout request for this sale has been sent. If it has, the
  /// server may have recorded it under [receiptNumber] even though we never
  /// saw the response, so that number can't be handed to a different sale.
  final bool checkoutAttempted;

  LocalCart copyWith({
    List<LocalCartLine>? lines,
    bool? seniorPwdApplied,
    Object? promoCode = _keep,
    Object? orderType = _keep,
    bool? serverBacked,
    Object? receiptNumber = _keep,
    bool? checkoutAttempted,
  }) => LocalCart(
    id: id,
    lines: lines ?? this.lines,
    seniorPwdApplied: seniorPwdApplied ?? this.seniorPwdApplied,
    promoCode: identical(promoCode, _keep) ? this.promoCode : promoCode as String?,
    orderType: identical(orderType, _keep) ? this.orderType : orderType as String?,
    serverBacked: serverBacked ?? this.serverBacked,
    saleId: saleId,
    receiptNumber:
        identical(receiptNumber, _keep) ? this.receiptNumber : receiptNumber as int?,
    checkoutAttempted: checkoutAttempted ?? this.checkoutAttempted,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'lines': [for (final line in lines) line.toJson()],
    'seniorPwdApplied': seniorPwdApplied,
    'promoCode': promoCode,
    'orderType': orderType,
    'serverBacked': serverBacked,
    'saleId': saleId,
    'receiptNumber': receiptNumber,
    'checkoutAttempted': checkoutAttempted,
  };

  static const _keep = Object();
}
