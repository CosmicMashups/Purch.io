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

  Future<Transaction> recordPayment(RecordPaymentRequest request);
}
