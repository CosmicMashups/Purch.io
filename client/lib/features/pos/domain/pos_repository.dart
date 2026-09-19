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

  /// Sends a whole sale — cart lines, discounts and payment — in one call.
  /// Idempotent on [CheckoutRequest.saleId].
  Future<Transaction> checkout(CheckoutRequest request);

  /// The highest receipt number the server has recorded for this terminal —
  /// the floor a device starts from when numbering its own sales.
  Future<int> getLastIssuedReceiptNumber();

  /// Kiosk orders submitted and awaiting a cashier to collect payment at the
  /// counter, for this branch.
  Future<List<Transaction>> listPendingKioskOrders(String branchId);

  /// Makes the given kiosk order this device's active Open cart, ready to
  /// pay through the normal checkout flow.
  Future<Transaction> claimKioskOrder(String transactionId);
}
