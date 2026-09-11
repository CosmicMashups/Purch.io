import '../../pos/domain/transaction_models.dart';

/// The kiosk's own cart engine — mechanically identical to PosRepository's
/// cart methods (same request/response shapes), but hits /kiosk/cart/* on
/// the backend, which is gated to Role.Kiosk rather than posOperator. No
/// void/payment/discount/promo here: those endpoints don't exist under
/// /kiosk at all, so there's nothing for a kiosk client to even call.
abstract class KioskCartRepository {
  Future<Transaction> getOrCreateOpenCart();

  Future<Transaction> addLine(AddTransactionLineRequest request);

  Future<Transaction> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  );

  Future<Transaction> removeLine(String lineId);

  Future<Transaction> setOrderType(SetOrderTypeRequest request);

  /// E6: closes the cart to further edits and issues its KioskPrepNumber.
  Future<Transaction> submitOrder();
}
