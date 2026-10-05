/// Mirrors Purch.Domain.Enums.PurchaseOrderStatus exactly, in declared order.
enum PurchaseOrderStatus { draft, sent, partiallyReceived, received, cancelled }

extension PurchaseOrderStatusLabel on PurchaseOrderStatus {
  String get label => switch (this) {
    PurchaseOrderStatus.draft => 'Draft',
    PurchaseOrderStatus.sent => 'Submitted',
    PurchaseOrderStatus.partiallyReceived => 'Partially Delivered',
    PurchaseOrderStatus.received => 'Delivered',
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
    this.receipts = const [],
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
      receipts:
          (json['receipts'] as List<dynamic>? ?? [])
              .cast<Map<String, dynamic>>()
              .map(PurchaseOrderReceipt.fromJson)
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

  /// Incoming Receiving Reports linked to this order.
  final List<PurchaseOrderReceipt> receipts;
}

/// Mirrors Purch.Application.Inventory.PurchaseOrderReceiptDto.
class PurchaseOrderReceipt {
  const PurchaseOrderReceipt({required this.reportId, required this.deliveryDate});

  factory PurchaseOrderReceipt.fromJson(Map<String, dynamic> json) {
    return PurchaseOrderReceipt(
      reportId: json['reportId'] as String,
      deliveryDate: json['deliveryDate'] as String,
    );
  }

  final String reportId;
  final String deliveryDate;
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
