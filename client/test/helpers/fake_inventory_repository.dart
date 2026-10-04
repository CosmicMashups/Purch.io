import 'package:purch_client/features/inventory/domain/inventory_item_models.dart';
import 'package:purch_client/features/inventory/domain/inventory_movement_models.dart';
import 'package:purch_client/features/inventory/domain/inventory_repository.dart';

class FakeInventoryRepository implements InventoryRepository {
  FakeInventoryRepository({
    this.recordMovementFailure,
    this.getDashboardFailure,
    List<InventoryMovement>? initialMovements,
    InventoryDashboard? initialDashboard,
  }) : movements = initialMovements ?? [],
       dashboard =
           initialDashboard ??
           const InventoryDashboard(
             totalSkus: 0,
             outOfStockCount: 0,
             lowStockCount: 0,
             lowStockItems: [],
           );

  final Object? recordMovementFailure;
  final Object? getDashboardFailure;
  final List<InventoryMovement> movements;
  final InventoryDashboard dashboard;

  RecordMovementRequest? lastRecordRequest;

  @override
  Future<InventoryDashboard> getDashboard() async {
    if (getDashboardFailure != null) {
      throw getDashboardFailure!;
    }
    return dashboard;
  }

  @override
  Future<List<InventoryMovement>> listMovements({
    String? itemId,
    String? branchId,
    MovementType? type,
    DateTime? before,
    String? beforeId,
    int? limit,
  }) async {
    final page = movements.where((movement) {
      if (itemId != null && movement.itemId != itemId) return false;
      if (branchId != null && movement.branchId != branchId) return false;
      if (type != null && movement.type != type) return false;
      if (before != null) {
        if (movement.createdAt.isAfter(before)) return false;
        if (movement.createdAt.isAtSameMomentAs(before) && beforeId != null) {
          if (movement.id.compareTo(beforeId) >= 0) return false;
        }
      }
      return true;
    }).toList()
      ..sort((a, b) {
        final c = b.createdAt.compareTo(a.createdAt);
        return c != 0 ? c : b.id.compareTo(a.id);
      });
    return limit == null ? page : page.take(limit).toList();
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

  final List<InventoryItem> inventoryItems = [];
  final Map<String, List<ItemRecipeLine>> recipesByItemId = {};

  @override
  Future<List<InventoryItem>> listInventoryItems() async => inventoryItems;

  @override
  Future<InventoryItem> createInventoryItem(
    CreateInventoryItemRequest request,
  ) async {
    final created = InventoryItem(
      id: 'inventory-item-${inventoryItems.length + 1}',
      name: request.name,
      sku: request.sku,
      baseUnit: request.baseUnit,
      packagingUnit: request.packagingUnit,
      packagingSize: request.packagingSize,
      quantityOnHand: 0,
      lowStockThreshold: request.lowStockThreshold,
      isAutoCreatedForItem: false,
      linkedItemId: null,
      isActive: true,
      categoryId: request.categoryId,
    );
    inventoryItems.add(created);
    return created;
  }

  @override
  Future<InventoryItem> updateInventoryItem(
    String id,
    UpdateInventoryItemRequest request,
  ) async {
    final index = inventoryItems.indexWhere((item) => item.id == id);
    final current = inventoryItems[index];
    final updated = InventoryItem(
      id: current.id,
      name: request.name,
      sku: request.sku,
      baseUnit: request.baseUnit,
      packagingUnit: request.packagingUnit,
      packagingSize: request.packagingSize,
      quantityOnHand: current.quantityOnHand,
      lowStockThreshold: request.lowStockThreshold,
      isAutoCreatedForItem: current.isAutoCreatedForItem,
      linkedItemId: current.linkedItemId,
      isActive: request.isActive,
      categoryId: request.categoryId,
    );
    inventoryItems[index] = updated;
    return updated;
  }

  @override
  Future<InventoryItem> updatePhysicalCount(
    String id,
    UpdatePhysicalCountRequest request,
  ) async {
    final index = inventoryItems.indexWhere((item) => item.id == id);
    final current = inventoryItems[index];
    final updated = InventoryItem(
      id: current.id,
      name: current.name,
      sku: current.sku,
      baseUnit: current.baseUnit,
      packagingUnit: current.packagingUnit,
      packagingSize: current.packagingSize,
      quantityOnHand: request.quantityOnHand,
      lowStockThreshold: current.lowStockThreshold,
      isAutoCreatedForItem: current.isAutoCreatedForItem,
      linkedItemId: current.linkedItemId,
      isActive: current.isActive,
    );
    inventoryItems[index] = updated;
    return updated;
  }

  @override
  Future<InventoryItem> receiveInventoryStock(
    String id,
    ReceiveInventoryStockRequest request,
  ) async {
    final index = inventoryItems.indexWhere((item) => item.id == id);
    final current = inventoryItems[index];
    final updated = InventoryItem(
      id: current.id,
      name: current.name,
      sku: current.sku,
      baseUnit: current.baseUnit,
      packagingUnit: current.packagingUnit,
      packagingSize: current.packagingSize,
      quantityOnHand:
          current.quantityOnHand + request.packagesReceived * current.packagingSize,
      lowStockThreshold: current.lowStockThreshold,
      isAutoCreatedForItem: current.isAutoCreatedForItem,
      linkedItemId: current.linkedItemId,
      isActive: current.isActive,
    );
    inventoryItems[index] = updated;
    return updated;
  }

  final List<InventoryCategory> inventoryCategories = [];

  @override
  Future<List<InventoryCategory>> listInventoryCategories() async =>
      inventoryCategories;

  @override
  Future<InventoryCategory> createInventoryCategory(
    InventoryCategoryRequest request,
  ) async {
    final created = InventoryCategory(
      id: 'inventory-category-${inventoryCategories.length + 1}',
      name: request.name,
      sortOrder: request.sortOrder,
    );
    inventoryCategories.add(created);
    return created;
  }

  @override
  Future<InventoryCategory> updateInventoryCategory(
    String id,
    InventoryCategoryRequest request,
  ) async {
    final index = inventoryCategories.indexWhere((c) => c.id == id);
    final updated = InventoryCategory(
      id: id,
      name: request.name,
      sortOrder: request.sortOrder,
    );
    inventoryCategories[index] = updated;
    return updated;
  }

  @override
  Future<void> deleteInventoryCategory(String id) async {
    inventoryCategories.removeWhere((c) => c.id == id);
    for (var i = 0; i < inventoryItems.length; i++) {
      final item = inventoryItems[i];
      if (item.categoryId == id) {
        inventoryItems[i] = InventoryItem(
          id: item.id,
          name: item.name,
          sku: item.sku,
          baseUnit: item.baseUnit,
          packagingUnit: item.packagingUnit,
          packagingSize: item.packagingSize,
          quantityOnHand: item.quantityOnHand,
          lowStockThreshold: item.lowStockThreshold,
          isAutoCreatedForItem: item.isAutoCreatedForItem,
          linkedItemId: item.linkedItemId,
          isActive: item.isActive,
        );
      }
    }
  }

  @override
  Future<List<ItemRecipeLine>> getItemRecipe(String itemId) async {
    return recipesByItemId[itemId] ?? [];
  }

  @override
  Future<List<ItemRecipeLine>> replaceItemRecipe(
    String itemId,
    ReplaceItemRecipeRequest request,
  ) async {
    final lines = [
      for (final line in request.lines)
        ItemRecipeLine(
          inventoryItemId: line.inventoryItemId,
          inventoryItemName: inventoryItems
              .firstWhere((item) => item.id == line.inventoryItemId)
              .name,
          quantityPerOrder: line.quantityPerOrder,
        ),
    ];
    recipesByItemId[itemId] = lines;
    return lines;
  }
}
