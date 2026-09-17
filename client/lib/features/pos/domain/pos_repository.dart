import 'transaction_models.dart';

/// D1's cart engine — a single in-progress Open transaction per device.
abstract class PosRepository {
  Future<Transaction> getOrCreateOpenCart();

  Future<Transaction> addLine(AddTransactionLineRequest request);

  Future<Transaction> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  );

  Future<Transaction> removeLine(String lineId);

  Future<Transaction> voidCart();

  Future<Transaction> applySeniorPwdDiscount(
    ApplySeniorPwdDiscountRequest request,
  );

  Future<Transaction> applyPromoCode(ApplyPromoCodeRequest request);

  Future<Transaction> setOrderType(SetOrderTypeRequest request);

  Future<Transaction> recordPayment(RecordPaymentRequest request);

  /// Kiosk orders submitted and awaiting a cashier to collect payment at the
  /// counter, for this branch.
  Future<List<Transaction>> listPendingKioskOrders(String branchId);

  /// Makes the given kiosk order this device's active Open cart, ready to
  /// pay through the normal checkout flow.
  Future<Transaction> claimKioskOrder(String transactionId);
}
