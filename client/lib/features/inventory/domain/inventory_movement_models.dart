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
      type: MovementType.values[json['type'] as int],
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
