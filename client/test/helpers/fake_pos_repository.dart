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
    this.applyPromoCodeFailure,
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
    promoCode: null,
    promoDiscountAmount: 0,
    totalAmount: 0,
    receiptNumber: null,
    payments: const [],
  );

  final Object? addLineFailure;
  final Object? updateLineFailure;
  final Object? removeLineFailure;
  final Object? voidCartFailure;
  final Object? applySeniorPwdDiscountFailure;
  final Object? applyPromoCodeFailure;
  final Object? recordPaymentFailure;

  Transaction cart;
  int voidCallCount = 0;
  int nextReceiptNumber = 1;
  RecordPaymentRequest? lastRecordPaymentRequest;
  AddTransactionLineRequest? lastAddLineRequest;
  ApplyPromoCodeRequest? lastApplyPromoCodeRequest;

  @override
  Future<Transaction> getOrCreateOpenCart() async => cart;

  @override
  Future<Transaction> addLine(AddTransactionLineRequest request) async {
    lastAddLineRequest = request;
    if (addLineFailure != null) {
      throw addLineFailure!;
    }

    final lines = [...cart.lines];
    final isCombo = request.comboSelections != null;
    final existingIndex =
        isCombo
            ? -1
            : lines.indexWhere(
              (line) =>
                  line.itemId == request.itemId &&
                  line.itemVariantId == request.itemVariantId,
            );
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
        comboSelections: existing.comboSelections,
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
          comboSelections:
              request.comboSelections
                  ?.map(
                    (selection) => TransactionLineComboSelection(
                      slotId: selection.slotId,
                      slotLabel: 'Slot ${selection.slotId}',
                      selectedItemId: selection.selectedItemId,
                      selectedItemName: 'Item ${selection.selectedItemId}',
                    ),
                  )
                  .toList() ??
              const [],
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
            comboSelections: line.comboSelections,
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
    final seniorAmount = request.apply ? subtotal * 0.2 : 0.0;
    cart = Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: cart.status,
      lines: cart.lines,
      subtotal: subtotal,
      discountAmount: seniorAmount + cart.promoDiscountAmount,
      seniorPwdDiscountApplied: request.apply,
      promoCode: cart.promoCode,
      promoDiscountAmount: cart.promoDiscountAmount,
      totalAmount: subtotal - seniorAmount - cart.promoDiscountAmount,
      receiptNumber: cart.receiptNumber,
      payments: cart.payments,
    );
    return cart;
  }

  @override
  Future<Transaction> applyPromoCode(ApplyPromoCodeRequest request) async {
    lastApplyPromoCodeRequest = request;
    if (applyPromoCodeFailure != null) {
      throw applyPromoCodeFailure!;
    }
    final subtotal = cart.subtotal;
    final seniorAmount = cart.seniorPwdDiscountApplied ? subtotal * 0.2 : 0.0;
    final promoAmount =
        (request.code == null || request.code!.isEmpty) ? 0.0 : subtotal * 0.1;
    cart = Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: cart.status,
      lines: cart.lines,
      subtotal: subtotal,
      discountAmount: seniorAmount + promoAmount,
      seniorPwdDiscountApplied: cart.seniorPwdDiscountApplied,
      promoCode: promoAmount > 0 ? request.code : null,
      promoDiscountAmount: promoAmount,
      totalAmount: subtotal - seniorAmount - promoAmount,
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
      promoCode: cart.promoCode,
      promoDiscountAmount: cart.promoDiscountAmount,
      totalAmount: cart.totalAmount,
      receiptNumber: nextReceiptNumber++,
      payments: [payment],
    );
    return cart;
  }

  Transaction _withLines(List<TransactionLine> lines) {
    final subtotal = lines.fold(0.0, (total, line) => total + line.lineTotal);
    final seniorAmount = cart.seniorPwdDiscountApplied ? subtotal * 0.2 : 0.0;
    final promoAmount = cart.promoCode != null ? subtotal * 0.1 : 0.0;
    return Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: cart.status,
      lines: lines,
      subtotal: subtotal,
      discountAmount: seniorAmount + promoAmount,
      seniorPwdDiscountApplied: cart.seniorPwdDiscountApplied,
      promoCode: cart.promoCode,
      promoDiscountAmount: promoAmount,
      totalAmount: subtotal - seniorAmount - promoAmount,
      receiptNumber: cart.receiptNumber,
      payments: cart.payments,
    );
  }
}
