import 'package:purch_client/features/kiosk/domain/kiosk_cart_repository.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

class FakeKioskCartRepository implements KioskCartRepository {
  FakeKioskCartRepository({Transaction? initialCart})
    : cart = initialCart ?? _emptyCart('cart-1');

  Transaction cart;
  bool submitCalled = false;

  static Transaction _emptyCart(String id) => Transaction(
    id: id,
    branchId: 'branch-1',
    deviceId: 'device-1',
    status: TransactionStatus.open,
    lines: const [],
    subtotal: 0,
    discountAmount: 0,
    seniorPwdDiscountApplied: false,
    promoCode: null,
    promoDiscountAmount: 0,
    totalAmount: 0,
    receiptNumber: null,
    payments: const [],
  );

  @override
  Future<Transaction> getOrCreateOpenCart() async => cart;

  @override
  Future<Transaction> addLine(AddTransactionLineRequest request) async {
    final line = TransactionLine(
      id: 'line-${cart.lines.length + 1}',
      itemId: request.itemId,
      itemName: 'Item ${request.itemId}',
      itemVariantId: request.itemVariantId,
      quantity: request.quantity,
      unitPrice: 10,
      lineTotal: 10 * request.quantity,
      comboSelections: const [],
    );
    cart = _withLines([...cart.lines, line]);
    return cart;
  }

  @override
  Future<Transaction> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  ) async {
    cart = _withLines([
      for (final line in cart.lines)
        if (line.id == lineId)
          TransactionLine(
            id: line.id,
            itemId: line.itemId,
            itemName: line.itemName,
            itemVariantId: line.itemVariantId,
            quantity: request.quantity,
            unitPrice: line.unitPrice,
            lineTotal: line.unitPrice * request.quantity,
            comboSelections: line.comboSelections,
          )
        else
          line,
    ]);
    return cart;
  }

  @override
  Future<Transaction> removeLine(String lineId) async {
    cart = _withLines(cart.lines.where((line) => line.id != lineId).toList());
    return cart;
  }

  @override
  Future<Transaction> setOrderType(SetOrderTypeRequest request) async {
    cart = Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: cart.status,
      lines: cart.lines,
      subtotal: cart.subtotal,
      discountAmount: cart.discountAmount,
      seniorPwdDiscountApplied: cart.seniorPwdDiscountApplied,
      promoCode: cart.promoCode,
      promoDiscountAmount: cart.promoDiscountAmount,
      totalAmount: cart.totalAmount,
      receiptNumber: cart.receiptNumber,
      orderType: request.orderType,
      payments: cart.payments,
    );
    return cart;
  }

  @override
  Future<Transaction> submitOrder() async {
    submitCalled = true;
    final submitted = Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: TransactionStatus.awaitingPayment,
      lines: cart.lines,
      subtotal: cart.subtotal,
      discountAmount: cart.discountAmount,
      seniorPwdDiscountApplied: cart.seniorPwdDiscountApplied,
      promoCode: cart.promoCode,
      promoDiscountAmount: cart.promoDiscountAmount,
      totalAmount: cart.totalAmount,
      receiptNumber: cart.receiptNumber,
      orderType: cart.orderType,
      originatedFromKiosk: true,
      kioskPrepNumber: 42,
      payments: cart.payments,
    );
    cart = _emptyCart('cart-2');
    return submitted;
  }

  Transaction _withLines(List<TransactionLine> lines) {
    final total = lines.fold<double>(0, (sum, line) => sum + line.lineTotal);
    return Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: cart.status,
      lines: lines,
      subtotal: total,
      discountAmount: 0,
      seniorPwdDiscountApplied: false,
      promoCode: null,
      promoDiscountAmount: 0,
      totalAmount: total,
      receiptNumber: cart.receiptNumber,
      orderType: cart.orderType,
      payments: cart.payments,
    );
  }
}
