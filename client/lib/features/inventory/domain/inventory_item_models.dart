/// Mirrors Purch.Application.Inventory.InventoryItemDto. Represents an
/// ingredient-level stock record (e.g. "soy sauce", "coffee beans") that a
/// Cashier Item can be backed by via a recipe/BOM, when the tenant has opted
/// into `useSeparateInventoryTracking`.
class InventoryItem {
  const InventoryItem({
    required this.id,
    required this.name,
    required this.sku,
    required this.baseUnit,
    required this.packagingUnit,
    required this.packagingSize,
    required this.quantityOnHand,
    required this.lowStockThreshold,
    required this.isAutoCreatedForItem,
    required this.linkedItemId,
    required this.isActive,
    this.categoryId,
    this.isCountedByHand = false,
  });

  factory InventoryItem.fromJson(Map<String, dynamic> json) {
    return InventoryItem(
      id: json['id'] as String,
      name: json['name'] as String,
      sku: json['sku'] as String?,
      baseUnit: json['baseUnit'] as String,
      packagingUnit: json['packagingUnit'] as String,
      packagingSize: (json['packagingSize'] as num).toDouble(),
      quantityOnHand: (json['quantityOnHand'] as num).toDouble(),
      lowStockThreshold: (json['lowStockThreshold'] as num?)?.toDouble(),
      isAutoCreatedForItem: json['isAutoCreatedForItem'] as bool,
      linkedItemId: json['linkedItemId'] as String?,
      isActive: json['isActive'] as bool,
      categoryId: json['categoryId'] as String?,
      isCountedByHand: json['isCountedByHand'] as bool? ?? false,
    );
  }

  final String id;
  final String name;
  final String? sku;
  final String baseUnit;
  final String packagingUnit;
  final double packagingSize;
  final double quantityOnHand;
  final double? lowStockThreshold;
  final bool isAutoCreatedForItem;
  final String? linkedItemId;
  final bool isActive;

  /// The ingredient category this is filed under; null when uncategorised.
  final String? categoryId;

  /// True when recipes use it but none deducts it on a sale, so only a count
  /// changes its stock (a sauce counted at the end of a shift, say).
  final bool isCountedByHand;

  /// How many whole packaging units (e.g. "cases", "packs") remain, given
  /// the raw base-unit quantity on hand.
  int get wholePackagesRemaining =>
      packagingSize <= 0 ? 0 : (quantityOnHand / packagingSize).floor();

  /// The leftover base-unit quantity that doesn't make up a whole packaging
  /// unit (e.g. loose grams left after counting whole kg packs).
  double get partialPackageRemainder =>
      packagingSize <= 0 ? quantityOnHand : quantityOnHand % packagingSize;
}

/// Mirrors Purch.Application.Inventory.CreateInventoryItemRequest.
class CreateInventoryItemRequest {
  const CreateInventoryItemRequest({
    required this.name,
    this.sku,
    required this.baseUnit,
    required this.packagingUnit,
    required this.packagingSize,
    this.lowStockThreshold,
    this.categoryId,
  });

  final String name;
  final String? sku;
  final String baseUnit;
  final String packagingUnit;
  final double packagingSize;
  final double? lowStockThreshold;
  final String? categoryId;

  Map<String, dynamic> toJson() => {
    'name': name,
    'sku': sku,
    'baseUnit': baseUnit,
    'packagingUnit': packagingUnit,
    'packagingSize': packagingSize,
    'lowStockThreshold': lowStockThreshold,
    'categoryId': categoryId,
  };
}

/// Mirrors Purch.Application.Inventory.UpdateInventoryItemRequest.
class UpdateInventoryItemRequest {
  const UpdateInventoryItemRequest({
    required this.name,
    this.sku,
    required this.baseUnit,
    required this.packagingUnit,
    required this.packagingSize,
    this.lowStockThreshold,
    required this.isActive,
    this.categoryId,
  });

  final String name;
  final String? sku;
  final String baseUnit;
  final String packagingUnit;
  final double packagingSize;
  final double? lowStockThreshold;
  final bool isActive;
  final String? categoryId;

  Map<String, dynamic> toJson() => {
    'name': name,
    'sku': sku,
    'baseUnit': baseUnit,
    'packagingUnit': packagingUnit,
    'packagingSize': packagingSize,
    'lowStockThreshold': lowStockThreshold,
    'isActive': isActive,
    'categoryId': categoryId,
  };
}

/// Mirrors Purch.Application.Inventory.InventoryCategoryDto: a group for
/// ingredients, kept apart from the item categories used at the till.
class InventoryCategory {
  const InventoryCategory({
    required this.id,
    required this.name,
    required this.sortOrder,
  });

  factory InventoryCategory.fromJson(Map<String, dynamic> json) {
    return InventoryCategory(
      id: json['id'] as String,
      name: json['name'] as String,
      sortOrder: json['sortOrder'] as int,
    );
  }

  final String id;
  final String name;
  final int sortOrder;
}

/// Mirrors Purch.Application.Inventory.CreateInventoryCategoryRequest and
/// UpdateInventoryCategoryRequest (same shape).
class InventoryCategoryRequest {
  const InventoryCategoryRequest({required this.name, required this.sortOrder});

  final String name;
  final int sortOrder;

  Map<String, dynamic> toJson() => {'name': name, 'sortOrder': sortOrder};
}

/// Mirrors Purch.Application.Inventory.UpdatePhysicalCountRequest.
class UpdatePhysicalCountRequest {
  const UpdatePhysicalCountRequest({
    required this.quantityOnHand,
    required this.branchId,
  });

  final double quantityOnHand;
  final String branchId;

  Map<String, dynamic> toJson() => {
    'quantityOnHand': quantityOnHand,
    'branchId': branchId,
  };
}

/// Mirrors Purch.Application.Inventory.ReceiveInventoryStockRequest.
class ReceiveInventoryStockRequest {
  const ReceiveInventoryStockRequest({
    required this.packagesReceived,
    required this.branchId,
    this.supplierReference,
  });

  final double packagesReceived;
  final String branchId;
  final String? supplierReference;

  Map<String, dynamic> toJson() => {
    'packagesReceived': packagesReceived,
    'branchId': branchId,
    'supplierReference': supplierReference,
  };
}

/// Mirrors Purch.Application.Inventory.ItemRecipeLineDto.
class ItemRecipeLine {
  const ItemRecipeLine({
    required this.inventoryItemId,
    required this.inventoryItemName,
    required this.quantityPerOrder,
  });

  factory ItemRecipeLine.fromJson(Map<String, dynamic> json) {
    return ItemRecipeLine(
      inventoryItemId: json['inventoryItemId'] as String,
      inventoryItemName: json['inventoryItemName'] as String,
      quantityPerOrder: (json['quantityPerOrder'] as num?)?.toDouble(),
    );
  }

  final String inventoryItemId;
  final String inventoryItemName;
  final double? quantityPerOrder;
}

/// Mirrors Purch.Application.Inventory.ReplaceItemRecipeLineRequest.
class ReplaceItemRecipeLineRequest {
  const ReplaceItemRecipeLineRequest({
    required this.inventoryItemId,
    this.quantityPerOrder,
  });

  final String inventoryItemId;
  final double? quantityPerOrder;

  Map<String, dynamic> toJson() => {
    'inventoryItemId': inventoryItemId,
    'quantityPerOrder': quantityPerOrder,
  };
}

/// Mirrors Purch.Application.Inventory.ReplaceItemRecipeRequest.
class ReplaceItemRecipeRequest {
  const ReplaceItemRecipeRequest({required this.lines});

  final List<ReplaceItemRecipeLineRequest> lines;

  Map<String, dynamic> toJson() => {
    'lines': lines.map((line) => line.toJson()).toList(),
  };
}
