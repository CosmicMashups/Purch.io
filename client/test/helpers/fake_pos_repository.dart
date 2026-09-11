import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/pos_repository.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

class FakePosRepository implements PosRepository {
  FakePosRepository({
    this.addLineFailure,
    this.updateLineFailure,
    this.removeLineFailure,
    this.voidCartFailure,
    this.applySeniorPwdDiscountFailure,
    this.recordPaymentFailure,
    Transaction? initialCart,
  }) : cart = initialCart ?? _emptyCart('cart-1');

  static Transaction _emptyCart(String id) => Transaction(
    id: id,
    branchId: 'branch-1',
    deviceId: 'device-1',
    status: TransactionStatus.open,
    lines: const [],
    subtotal: 0,
    discountAmount: 0,
    seniorPwdDiscountApplied: false,
    totalAmount: 0,
    receiptNumber: null,
    payments: const [],
  );

  final Object? addLineFailure;
  final Object? updateLineFailure;
  final Object? removeLineFailure;
  final Object? voidCartFailure;
  final Object? applySeniorPwdDiscountFailure;
  final Object? recordPaymentFailure;

  Transaction cart;
  int voidCallCount = 0;
  int nextReceiptNumber = 1;
  RecordPaymentRequest? lastRecordPaymentRequest;

  @override
  Future<Transaction> getOrCreateOpenCart() async => cart;

  @override
  Future<Transaction> addLine(AddTransactionLineRequest request) async {
    if (addLineFailure != null) {
      throw addLineFailure!;
    }
    final existingIndex = cart.lines.indexWhere(
      (line) =>
          line.itemId == request.itemId &&
          line.itemVariantId == request.itemVariantId,
    );
    final lines = [...cart.lines];
    if (existingIndex >= 0) {
      final existing = lines[existingIndex];
      final newQuantity = existing.quantity + request.quantity;
      lines[existingIndex] = TransactionLine(
        id: existing.id,
        itemId: existing.itemId,
        itemName: existing.itemName,
        itemVariantId: existing.itemVariantId,
        quantity: newQuantity,
        unitPrice: existing.unitPrice,
        lineTotal: existing.unitPrice * newQuantity,
      );
    } else {
      lines.add(
        TransactionLine(
          id: 'line-${lines.length + 1}',
          itemId: request.itemId,
          itemName: 'Item ${request.itemId}',
          itemVariantId: request.itemVariantId,
          quantity: request.quantity,
          unitPrice: 10,
          lineTotal: 10 * request.quantity,
        ),
      );
    }
    cart = _withLines(lines);
    return cart;
  }

  @override
  Future<Transaction> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  ) async {
    if (updateLineFailure != null) {
      throw updateLineFailure!;
    }
    final lines =
        cart.lines.map((line) {
          if (line.id != lineId) {
            return line;
          }
          return TransactionLine(
            id: line.id,
            itemId: line.itemId,
            itemName: line.itemName,
            itemVariantId: line.itemVariantId,
            quantity: request.quantity,
            unitPrice: line.unitPrice,
            lineTotal: line.unitPrice * request.quantity,
          );
        }).toList();
    cart = _withLines(lines);
    return cart;
  }

  @override
  Future<Transaction> removeLine(String lineId) async {
    if (removeLineFailure != null) {
      throw removeLineFailure!;
    }
    final lines = cart.lines.where((line) => line.id != lineId).toList();
    cart = _withLines(lines);
    return cart;
  }

  @override
  Future<Transaction> voidCart() async {
    voidCallCount++;
    if (voidCartFailure != null) {
      throw voidCartFailure!;
    }
    cart = _emptyCart('cart-${voidCallCount + 1}');
    return cart;
  }

  @override
  Future<Transaction> applySeniorPwdDiscount(
    ApplySeniorPwdDiscountRequest request,
  ) async {
    if (applySeniorPwdDiscountFailure != null) {
      throw applySeniorPwdDiscountFailure!;
    }
    final subtotal = cart.subtotal;
    final discountAmount = request.apply ? subtotal * 0.2 : 0.0;
    cart = Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: cart.status,
      lines: cart.lines,
      subtotal: subtotal,
      discountAmount: discountAmount,
      seniorPwdDiscountApplied: request.apply,
      totalAmount: subtotal - discountAmount,
      receiptNumber: cart.receiptNumber,
      payments: cart.payments,
    );
    return cart;
  }

  @override
  Future<Transaction> recordPayment(RecordPaymentRequest request) async {
    lastRecordPaymentRequest = request;
    if (recordPaymentFailure != null) {
      throw recordPaymentFailure!;
    }
    final changeGiven =
        request.method == PaymentMethod.cash && request.amountTendered != null
            ? request.amountTendered! - cart.totalAmount
            : null;
    final payment = Payment(
      id: 'payment-1',
      method: request.method,
      status: PaymentStatus.confirmed,
      amount: cart.totalAmount,
      amountTendered: request.amountTendered,
      changeGiven: changeGiven,
    );
    cart = Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: TransactionStatus.completed,
      lines: cart.lines,
      subtotal: cart.subtotal,
      discountAmount: cart.discountAmount,
      seniorPwdDiscountApplied: cart.seniorPwdDiscountApplied,
      totalAmount: cart.totalAmount,
      receiptNumber: nextReceiptNumber++,
      payments: [payment],
    );
    return cart;
  }

  Transaction _withLines(List<TransactionLine> lines) {
    final subtotal = lines.fold(0.0, (total, line) => total + line.lineTotal);
    final discountAmount = cart.seniorPwdDiscountApplied ? subtotal * 0.2 : 0.0;
    return Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: cart.status,
      lines: lines,
      subtotal: subtotal,
      discountAmount: discountAmount,
      seniorPwdDiscountApplied: cart.seniorPwdDiscountApplied,
      totalAmount: subtotal - discountAmount,
      receiptNumber: cart.receiptNumber,
      payments: cart.payments,
    );
  }
}
