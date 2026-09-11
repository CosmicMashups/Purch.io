import 'package:purch_client/features/inventory/domain/inventory_movement_models.dart';
import 'package:purch_client/features/inventory/domain/inventory_repository.dart';

class FakeInventoryRepository implements InventoryRepository {
  FakeInventoryRepository({
    this.recordMovementFailure,
    List<InventoryMovement>? initialMovements,
  }) : movements = initialMovements ?? [];

  final Object? recordMovementFailure;
  final List<InventoryMovement> movements;

  RecordMovementRequest? lastRecordRequest;

  @override
  Future<List<InventoryMovement>> listMovements({
    String? itemId,
    String? branchId,
    MovementType? type,
  }) async {
    return movements.where((movement) {
      if (itemId != null && movement.itemId != itemId) return false;
      if (branchId != null && movement.branchId != branchId) return false;
      if (type != null && movement.type != type) return false;
      return true;
    }).toList();
  }

  @override
  Future<InventoryMovement> recordMovement(
    RecordMovementRequest request,
  ) async {
    lastRecordRequest = request;
    if (recordMovementFailure != null) {
      throw recordMovementFailure!;
    }
    final created = InventoryMovement(
      id: 'movement-${movements.length + 1}',
      itemId: request.itemId,
      itemName: 'Item ${request.itemId}',
      branchId: request.branchId,
      branchName: 'Branch ${request.branchId}',
      type: request.type,
      quantity: request.quantity,
      staffUserId: 'user-1',
      staffUserName: 'Admin User',
      note: request.note,
      reasonCategory: request.reasonCategory,
      photoUrl: request.photoUrl,
      supplierReference: request.supplierReference,
      createdAt: DateTime(2026, 1, 1),
    );
    movements.add(created);
    return created;
  }
}
