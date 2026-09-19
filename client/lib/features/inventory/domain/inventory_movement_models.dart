/// Mirrors Purch.Domain.Enums.MovementType exactly, in declared order.
enum MovementType {
  stockIn,
  stockOut,
  consumption,
  spoiled,
  damaged,
  forReturn,
  transfer,
  adjustment,

  /// System-generated only, from a completed Cashier sale — never manually
  /// selectable when recording a movement by hand.
  sale,

  /// Client-only fallback for a type this build doesn't know yet (e.g. the
  /// server added a new movement type) — decoding one must never throw and
  /// blank a whole screen. Never sent to the server or offered in pickers.
  other;

  /// Decodes the server's numeric enum value, tolerating out-of-range values.
  static MovementType fromServer(Object? value) {
    final index = value is int ? value : -1;
    return index >= 0 && index < MovementType.other.index
        ? MovementType.values[index]
        : MovementType.other;
  }

  /// The types a person can pick from when filtering or recording by hand.
  static List<MovementType> get selectable =>
      values.where((type) => type != MovementType.other).toList();
}

extension MovementTypeLabel on MovementType {
  String get label => switch (this) {
    MovementType.stockIn => 'Stock-In',
    MovementType.stockOut => 'Stock-Out',
    MovementType.consumption => 'Consumption',
    MovementType.spoiled => 'Spoiled',
    MovementType.damaged => 'Damaged',
    MovementType.forReturn => 'For Return',
    MovementType.transfer => 'Transfer',
    MovementType.adjustment => 'Adjustment',
    MovementType.sale => 'Sale',
    MovementType.other => 'Other',
  };
}

/// Mirrors Purch.Application.Inventory.InventoryMovementDto.
class InventoryMovement {
  const InventoryMovement({
    required this.id,
    required this.itemId,
    required this.itemName,
    required this.branchId,
    required this.branchName,
    required this.type,
    required this.quantity,
    required this.staffUserId,
    required this.staffUserName,
    required this.note,
    required this.reasonCategory,
    required this.photoUrl,
    required this.supplierReference,
    required this.createdAt,
  });

  factory InventoryMovement.fromJson(Map<String, dynamic> json) {
    return InventoryMovement(
      id: json['id'] as String,
      itemId: json['itemId'] as String,
      itemName: json['itemName'] as String,
      branchId: json['branchId'] as String,
      branchName: json['branchName'] as String,
      type: MovementType.fromServer(json['type']),
      quantity: (json['quantity'] as num).toDouble(),
      staffUserId: json['staffUserId'] as String,
      staffUserName: json['staffUserName'] as String,
      note: json['note'] as String?,
      reasonCategory: json['reasonCategory'] as String?,
      photoUrl: json['photoUrl'] as String?,
      supplierReference: json['supplierReference'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }

  final String id;
  final String itemId;
  final String itemName;
  final String branchId;
  final String branchName;
  final MovementType type;
  final double quantity;
  final String staffUserId;
  final String staffUserName;
  final String? note;
  final String? reasonCategory;
  final String? photoUrl;
  final String? supplierReference;
  final DateTime createdAt;
}

/// Mirrors Purch.Application.Inventory.RecordMovementRequest. Quantity is
/// always positive except for Adjustment, where a negative value corrects
/// stock downward.
class RecordMovementRequest {
  const RecordMovementRequest({
    required this.itemId,
    required this.branchId,
    required this.type,
    required this.quantity,
    this.note,
    this.reasonCategory,
    this.photoUrl,
    this.supplierReference,
  });

  final String itemId;
  final String branchId;
  final MovementType type;
  final double quantity;
  final String? note;
  final String? reasonCategory;
  final String? photoUrl;
  final String? supplierReference;

  Map<String, dynamic> toJson() => {
    'itemId': itemId,
    'branchId': branchId,
    'type': type.index,
    'quantity': quantity,
    'note': note,
    'reasonCategory': reasonCategory,
    'photoUrl': photoUrl,
    'supplierReference': supplierReference,
  };
}

/// Mirrors Purch.Application.Inventory.InventoryDashboardDto — C1's overview
/// cards + low-stock alert list.
class InventoryDashboard {
  const InventoryDashboard({
    required this.totalSkus,
    required this.outOfStockCount,
    required this.lowStockCount,
    required this.lowStockItems,
  });

  factory InventoryDashboard.fromJson(Map<String, dynamic> json) {
    return InventoryDashboard(
      totalSkus: json['totalSkus'] as int,
      outOfStockCount: json['outOfStockCount'] as int,
      lowStockCount: json['lowStockCount'] as int,
      lowStockItems:
          (json['lowStockItems'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(LowStockItem.fromJson)
              .toList(),
    );
  }

  final int totalSkus;
  final int outOfStockCount;
  final int lowStockCount;
  final List<LowStockItem> lowStockItems;
}

/// Mirrors Purch.Application.Inventory.LowStockItemDto.
class LowStockItem {
  const LowStockItem({
    required this.itemId,
    required this.itemName,
    required this.stockOnHand,
    required this.lowStockThreshold,
  });

  factory LowStockItem.fromJson(Map<String, dynamic> json) {
    return LowStockItem(
      itemId: json['itemId'] as String,
      itemName: json['itemName'] as String,
      stockOnHand: (json['stockOnHand'] as num).toDouble(),
      lowStockThreshold: (json['lowStockThreshold'] as num).toDouble(),
    );
  }

  final String itemId;
  final String itemName;
  final double stockOnHand;
  final double lowStockThreshold;
}
