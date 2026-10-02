import 'exchange_models.dart';
import 'transaction_models.dart';

/// D1's cart engine — a single in-progress Open transaction per device.
abstract class PosRepository {
  Future<Transaction> getOrCreateOpenCart();

  Future<Transaction> addLine(AddTransactionLineRequest request);

  Future<Transaction> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  );

  Future<Transaction> removeLine(String lineId, {String? approverPin});

  Future<Transaction> voidCart({String? approverPin});

  Future<Transaction> applySeniorPwdDiscount(
    ApplySeniorPwdDiscountRequest request,
  );

  Future<Transaction> applyPromoCode(ApplyPromoCodeRequest request);

  Future<Transaction> setOrderType(SetOrderTypeRequest request);

  Future<Transaction> recordPayment(RecordPaymentRequest request);

  /// Refunds a completed sale, marking it Refunded and auditing the action. Always needs a manager/
  /// admin's approval — see RefundTransactionRequest.
  Future<Transaction> refundTransaction(
    String transactionId,
    RefundTransactionRequest request,
  );

  /// Finds completed sales by receipt number, so one the cashier doesn't already have open (an earlier
  /// day, a different terminal) can still be refunded or exchanged. Receipt numbers are only unique per
  /// device, so this can come back with more than one match.
  Future<List<Transaction>> findByReceiptNumber(int receiptNumber);

  /// Records an exchange against a completed sale: some of its lines come back, other items go out, and
  /// the price difference is settled. Always needs a manager/admin's approval — see [ExchangeRequest].
  Future<Adjustment> createExchange(
    String transactionId,
    ExchangeRequest request,
  );

  /// How much of each line of a completed sale can still be returned, keyed by line id (what was bought,
  /// less what earlier exchanges already took back).
  Future<Map<String, double>> listReturnableLines(String transactionId);

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
