import 'inventory_item_models.dart';
import 'inventory_movement_models.dart';

/// C2/C3 — the stock movement log and the form that records into it.
abstract class InventoryRepository {
  Future<InventoryDashboard> getDashboard();

  Future<List<InventoryMovement>> listMovements({
    String? itemId,
    String? branchId,
    MovementType? type,
    DateTime? before,
    int? limit,
  });

  Future<InventoryMovement> recordMovement(RecordMovementRequest request);

  // Ingredient-level inventory tracking (opt-in per tenant via
  // useSeparateInventoryTracking).

  Future<List<InventoryItem>> listInventoryItems();

  Future<InventoryItem> createInventoryItem(
    CreateInventoryItemRequest request,
  );

  Future<InventoryItem> updateInventoryItem(
    String id,
    UpdateInventoryItemRequest request,
  );

  Future<InventoryItem> updatePhysicalCount(
    String id,
    UpdatePhysicalCountRequest request,
  );

  Future<InventoryItem> receiveInventoryStock(
    String id,
    ReceiveInventoryStockRequest request,
  );

  Future<List<ItemRecipeLine>> getItemRecipe(String itemId);

  Future<List<ItemRecipeLine>> replaceItemRecipe(
    String itemId,
    ReplaceItemRecipeRequest request,
  );
}
