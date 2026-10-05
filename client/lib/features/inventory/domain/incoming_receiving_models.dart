/// Mirrors Purch.Domain.Enums.ReceivingCondition.
enum ReceivingCondition { good, notGood }

extension ReceivingConditionLabel on ReceivingCondition {
  String get label => switch (this) {
    ReceivingCondition.good => 'Good',
    ReceivingCondition.notGood => 'Not Good',
  };
}

/// Mirrors Purch.Domain.Enums.ReceivingRemark.
enum ReceivingRemark { accepted, rejected }

extension ReceivingRemarkLabel on ReceivingRemark {
  String get label => switch (this) {
    ReceivingRemark.accepted => 'Accepted',
    ReceivingRemark.rejected => 'Rejected',
  };
}

/// Mirrors Purch.Application.Inventory.IncomingReceivingLineDto.
class IncomingReceivingLine {
  const IncomingReceivingLine({
    required this.id,
    required this.itemId,
    required this.itemName,
    required this.quantityReceived,
    required this.uom,
    required this.unitPrice,
    required this.condition,
    required this.remark,
  });

  factory IncomingReceivingLine.fromJson(Map<String, dynamic> json) {
    return IncomingReceivingLine(
      id: json['id'] as String,
      itemId: json['itemId'] as String,
      itemName: json['itemName'] as String,
      quantityReceived: (json['quantityReceived'] as num).toDouble(),
      uom: json['uom'] as String,
      unitPrice: (json['unitPrice'] as num).toDouble(),
      condition: ReceivingCondition.values[json['condition'] as int],
      remark: ReceivingRemark.values[json['remark'] as int],
    );
  }

  final String id;
  final String itemId;
  final String itemName;
  final double quantityReceived;
  final String uom;
  final double unitPrice;
  final ReceivingCondition condition;
  final ReceivingRemark remark;
}

/// Mirrors Purch.Application.Inventory.IncomingReceivingDto.
class IncomingReceiving {
  const IncomingReceiving({
    required this.id,
    required this.purchaseOrderId,
    required this.supplierId,
    required this.supplierName,
    required this.branchId,
    required this.branchName,
    required this.receivedByName,
    required this.deliveryDate,
    required this.remarks,
    required this.lines,
  });

  factory IncomingReceiving.fromJson(Map<String, dynamic> json) {
    return IncomingReceiving(
      id: json['id'] as String,
      purchaseOrderId: json['purchaseOrderId'] as String?,
      supplierId: json['supplierId'] as String,
      supplierName: json['supplierName'] as String,
      branchId: json['branchId'] as String,
      branchName: json['branchName'] as String,
      receivedByName: json['receivedByName'] as String,
      deliveryDate: json['deliveryDate'] as String,
      remarks: json['remarks'] as String?,
      lines:
          (json['lines'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(IncomingReceivingLine.fromJson)
              .toList(),
    );
  }

  final String id;

  /// Null until an admin links the report to a purchase order.
  final String? purchaseOrderId;
  final String supplierId;
  final String supplierName;
  final String branchId;
  final String branchName;
  final String receivedByName;
  final String deliveryDate;
  final String? remarks;
  final List<IncomingReceivingLine> lines;
}

/// Mirrors Purch.Application.Inventory.CreateIncomingReceivingRequest.
class CreateIncomingReceivingRequest {
  const CreateIncomingReceivingRequest({
    required this.purchaseOrderId,
    required this.supplierId,
    required this.branchId,
    required this.deliveryDate,
    required this.remarks,
    required this.lines,
  });

  final String? purchaseOrderId;
  final String supplierId;
  final String branchId;

  /// yyyy-MM-dd.
  final String deliveryDate;
  final String? remarks;
  final List<CreateIncomingReceivingLineRequest> lines;

  Map<String, dynamic> toJson() => {
    'purchaseOrderId': purchaseOrderId,
    'supplierId': supplierId,
    'branchId': branchId,
    'deliveryDate': deliveryDate,
    'remarks': remarks,
    'lines': lines.map((line) => line.toJson()).toList(),
  };
}

/// Mirrors Purch.Application.Inventory.CreateIncomingReceivingLineRequest.
class CreateIncomingReceivingLineRequest {
  const CreateIncomingReceivingLineRequest({
    required this.itemId,
    required this.quantityReceived,
    required this.uom,
    required this.unitPrice,
    required this.condition,
    required this.remark,
  });

  final String itemId;
  final double quantityReceived;
  final String uom;
  final double unitPrice;
  final ReceivingCondition condition;
  final ReceivingRemark remark;

  Map<String, dynamic> toJson() => {
    'itemId': itemId,
    'quantityReceived': quantityReceived,
    'uom': uom,
    'unitPrice': unitPrice,
    'condition': condition.index,
    'remark': remark.index,
  };
}
