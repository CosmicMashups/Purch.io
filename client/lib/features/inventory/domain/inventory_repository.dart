import 'inventory_movement_models.dart';

/// C2/C3 — the stock movement log and the form that records into it.
abstract class InventoryRepository {
  Future<InventoryDashboard> getDashboard();

  Future<List<InventoryMovement>> listMovements({
    String? itemId,
    String? branchId,
    MovementType? type,
  });

  Future<InventoryMovement> recordMovement(RecordMovementRequest request);
}
