/// Mirrors Purch.Domain.Enums.PurchaseOrderStatus exactly, in declared order.
enum PurchaseOrderStatus { draft, sent, partiallyReceived, received, cancelled }

extension PurchaseOrderStatusLabel on PurchaseOrderStatus {
  String get label => switch (this) {
    PurchaseOrderStatus.draft => 'Draft',
    PurchaseOrderStatus.sent => 'Sent',
    PurchaseOrderStatus.partiallyReceived => 'Partially Received',
    PurchaseOrderStatus.received => 'Received',
    PurchaseOrderStatus.cancelled => 'Cancelled',
  };
}

/// Mirrors Purch.Application.Inventory.PurchaseOrderDto.
class PurchaseOrder {
  const PurchaseOrder({
    required this.id,
    required this.supplierId,
    required this.supplierName,
    required this.branchId,
    required this.branchName,
    required this.status,
    required this.sentAt,
    required this.lines,
  });

  factory PurchaseOrder.fromJson(Map<String, dynamic> json) {
    return PurchaseOrder(
      id: json['id'] as String,
      supplierId: json['supplierId'] as String,
      supplierName: json['supplierName'] as String,
      branchId: json['branchId'] as String,
      branchName: json['branchName'] as String,
      status: PurchaseOrderStatus.values[json['status'] as int],
      sentAt:
          json['sentAt'] == null
              ? null
              : DateTime.parse(json['sentAt'] as String),
      lines:
          (json['lines'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(PurchaseOrderLine.fromJson)
              .toList(),
    );
  }

  final String id;
  final String supplierId;
  final String supplierName;
  final String branchId;
  final String branchName;
  final PurchaseOrderStatus status;
  final DateTime? sentAt;
  final List<PurchaseOrderLine> lines;
}

/// Mirrors Purch.Application.Inventory.PurchaseOrderLineDto.
class PurchaseOrderLine {
  const PurchaseOrderLine({
    required this.id,
    required this.itemId,
    required this.itemName,
    required this.quantityOrdered,
    required this.quantityReceived,
    required this.expectedUnitCost,
  });

  factory PurchaseOrderLine.fromJson(Map<String, dynamic> json) {
    return PurchaseOrderLine(
      id: json['id'] as String,
      itemId: json['itemId'] as String,
      itemName: json['itemName'] as String,
      quantityOrdered: (json['quantityOrdered'] as num).toDouble(),
      quantityReceived: (json['quantityReceived'] as num).toDouble(),
      expectedUnitCost: (json['expectedUnitCost'] as num).toDouble(),
    );
  }

  final String id;
  final String itemId;
  final String itemName;
  final double quantityOrdered;
  final double quantityReceived;
  final double expectedUnitCost;

  double get quantityRemaining => quantityOrdered - quantityReceived;
}

/// Mirrors Purch.Application.Inventory.CreatePurchaseOrderRequest.
class CreatePurchaseOrderRequest {
  const CreatePurchaseOrderRequest({
    required this.supplierId,
    required this.branchId,
    required this.lines,
  });

  final String supplierId;
  final String branchId;
  final List<CreatePurchaseOrderLineRequest> lines;

  Map<String, dynamic> toJson() => {
    'supplierId': supplierId,
    'branchId': branchId,
    'lines': lines.map((line) => line.toJson()).toList(),
  };
}

/// Mirrors Purch.Application.Inventory.CreatePurchaseOrderLineRequest.
class CreatePurchaseOrderLineRequest {
  const CreatePurchaseOrderLineRequest({
    required this.itemId,
    required this.quantityOrdered,
    required this.expectedUnitCost,
  });

  final String itemId;
  final double quantityOrdered;
  final double expectedUnitCost;

  Map<String, dynamic> toJson() => {
    'itemId': itemId,
    'quantityOrdered': quantityOrdered,
    'expectedUnitCost': expectedUnitCost,
  };
}

/// Mirrors Purch.Application.Inventory.ReceivePurchaseOrderRequest. A PO can
/// be received across several partial deliveries — each entry adds on top
/// of that line's running received total.
class ReceivePurchaseOrderRequest {
  const ReceivePurchaseOrderRequest({required this.lines});

  final List<ReceivePurchaseOrderLineRequest> lines;

  Map<String, dynamic> toJson() => {
    'lines': lines.map((line) => line.toJson()).toList(),
  };
}

/// Mirrors Purch.Application.Inventory.ReceivePurchaseOrderLineRequest.
class ReceivePurchaseOrderLineRequest {
  const ReceivePurchaseOrderLineRequest({
    required this.lineId,
    required this.receivedQuantity,
  });

  final String lineId;
  final double receivedQuantity;

  Map<String, dynamic> toJson() => {
    'lineId': lineId,
    'receivedQuantity': receivedQuantity,
  };
}
