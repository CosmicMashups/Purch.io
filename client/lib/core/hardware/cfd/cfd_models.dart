import '../../../features/pos/domain/payment_method.dart';

enum CfdMode {
  idle,
  cart,
  payment,
  completed,
}

class CfdLineItem {
  const CfdLineItem({
    required this.name,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });

  final String name;
  final double quantity;
  final double unitPrice;
  final double lineTotal;

  Map<String, dynamic> toJson() => {
        'name': name,
        'quantity': quantity,
        'unitPrice': unitPrice,
        'lineTotal': lineTotal,
      };

  factory CfdLineItem.fromJson(Map<String, dynamic> json) => CfdLineItem(
        name: json['name'] as String,
        quantity: (json['quantity'] as num).toDouble(),
        unitPrice: (json['unitPrice'] as num).toDouble(),
        lineTotal: (json['lineTotal'] as num).toDouble(),
      );
}

class CfdState {
  const CfdState({
    this.mode = CfdMode.idle,
    this.storeName = 'Purch.io Store',
    this.storeLogoUrl,
    this.welcomeMessage = 'Welcome! Maligayang Pagdating!',
    this.lines = const [],
    this.subtotal = 0.0,
    this.discountAmount = 0.0,
    this.totalAmount = 0.0,
    this.vatableSales = 0.0,
    this.vatAmount = 0.0,
    this.paymentMethod,
    this.qrPhPayload,
    this.amountTendered,
    this.changeGiven,
    this.receiptNumber,
  });

  final CfdMode mode;
  final String storeName;
  final String? storeLogoUrl;
  final String welcomeMessage;
  final List<CfdLineItem> lines;
  final double subtotal;
  final double discountAmount;
  final double totalAmount;

  /// VAT-inclusive sale amount excluding the 12% VAT component. Zero when
  /// the transaction is VAT-exempt (senior/PWD discount applied).
  final double vatableSales;

  /// The 12% VAT component of [totalAmount]. Zero when VAT-exempt.
  final double vatAmount;
  final PaymentMethod? paymentMethod;
  final String? qrPhPayload;
  final double? amountTendered;
  final double? changeGiven;
  final int? receiptNumber;

  Map<String, dynamic> toJson() => {
        'mode': mode.name,
        'storeName': storeName,
        'storeLogoUrl': storeLogoUrl,
        'welcomeMessage': welcomeMessage,
        'lines': lines.map((l) => l.toJson()).toList(),
        'subtotal': subtotal,
        'discountAmount': discountAmount,
        'totalAmount': totalAmount,
        'vatableSales': vatableSales,
        'vatAmount': vatAmount,
        'paymentMethod': paymentMethod?.name,
        'qrPhPayload': qrPhPayload,
        'amountTendered': amountTendered,
        'changeGiven': changeGiven,
        'receiptNumber': receiptNumber,
      };

  CfdState copyWith({
    CfdMode? mode,
    String? storeName,
    String? storeLogoUrl,
    String? welcomeMessage,
    List<CfdLineItem>? lines,
    double? subtotal,
    double? discountAmount,
    double? totalAmount,
    double? vatableSales,
    double? vatAmount,
    PaymentMethod? paymentMethod,
    String? qrPhPayload,
    double? amountTendered,
    double? changeGiven,
    int? receiptNumber,
  }) {
    return CfdState(
      mode: mode ?? this.mode,
      storeName: storeName ?? this.storeName,
      storeLogoUrl: storeLogoUrl ?? this.storeLogoUrl,
      welcomeMessage: welcomeMessage ?? this.welcomeMessage,
      lines: lines ?? this.lines,
      subtotal: subtotal ?? this.subtotal,
      discountAmount: discountAmount ?? this.discountAmount,
      totalAmount: totalAmount ?? this.totalAmount,
      vatableSales: vatableSales ?? this.vatableSales,
      vatAmount: vatAmount ?? this.vatAmount,
      paymentMethod: paymentMethod ?? this.paymentMethod,
      qrPhPayload: qrPhPayload ?? this.qrPhPayload,
      amountTendered: amountTendered ?? this.amountTendered,
      changeGiven: changeGiven ?? this.changeGiven,
      receiptNumber: receiptNumber ?? this.receiptNumber,
    );
  }
}
