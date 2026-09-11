import 'package:purch_client/features/pos/domain/pos_repository.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

class FakePosRepository implements PosRepository {
  FakePosRepository({
    this.addLineFailure,
    this.updateLineFailure,
    this.removeLineFailure,
    this.voidCartFailure,
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
    totalAmount: 0,
  );

  final Object? addLineFailure;
  final Object? updateLineFailure;
  final Object? removeLineFailure;
  final Object? voidCartFailure;

  Transaction cart;
  int voidCallCount = 0;

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

  Transaction _withLines(List<TransactionLine> lines) {
    final subtotal = lines.fold(0.0, (total, line) => total + line.lineTotal);
    return Transaction(
      id: cart.id,
      branchId: cart.branchId,
      deviceId: cart.deviceId,
      status: cart.status,
      lines: lines,
      subtotal: subtotal,
      discountAmount: cart.discountAmount,
      totalAmount: subtotal - cart.discountAmount,
    );
  }
}
