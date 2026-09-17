import '../../pos/domain/transaction_models.dart';

/// Pairs a Kitchen Display terminal and reads the live queue of pending
/// kiosk orders (with their line items) for kitchen staff to prepare food
/// against, branch-wide.
abstract class KitchenDisplayRepository {
  Future<void> pair({required String devicePairingCode, required String pairingPin});

  /// This device's own branch, decoded from its stored session token — null
  /// if no session is stored yet.
  Future<String?> currentBranchId();

  Future<List<Transaction>> listPendingOrders(String branchId);

  /// Advances a kiosk order's kitchen-prep state (queued/preparing/ready/pickedUp).
  Future<Transaction> updateStatus(String transactionId, KitchenStatus status);
}
