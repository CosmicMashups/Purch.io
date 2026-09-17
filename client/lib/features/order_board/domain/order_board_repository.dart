import '../../pos/domain/transaction_models.dart';

/// Pairs an Order Number Board terminal and reads the live queue of pending
/// kiosk orders for it to display, branch-wide, so a cashier calling out a
/// number matches what customers see on the board.
abstract class OrderBoardRepository {
  Future<void> pair({required String devicePairingCode, required String pairingPin});

  /// This device's own branch, decoded from its stored session token — null
  /// if no session is stored yet.
  Future<String?> currentBranchId();

  Future<List<Transaction>> listPendingOrders(String branchId);
}
