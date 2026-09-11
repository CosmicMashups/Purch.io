/// Mirrors Purch.Domain.Enums.BranchTransferStatus exactly, in declared order.
enum BranchTransferStatus { pending, inTransit, received }

extension BranchTransferStatusLabel on BranchTransferStatus {
  String get label => switch (this) {
    BranchTransferStatus.pending => 'Pending',
    BranchTransferStatus.inTransit => 'In Transit',
    BranchTransferStatus.received => 'Received',
  };
}

/// Mirrors Purch.Application.Inventory.BranchTransferDto.
class BranchTransfer {
  const BranchTransfer({
    required this.id,
    required this.sourceBranchId,
    required this.sourceBranchName,
    required this.destinationBranchId,
    required this.destinationBranchName,
    required this.status,
    required this.lines,
  });

  factory BranchTransfer.fromJson(Map<String, dynamic> json) {
    return BranchTransfer(
      id: json['id'] as String,
      sourceBranchId: json['sourceBranchId'] as String,
      sourceBranchName: json['sourceBranchName'] as String,
      destinationBranchId: json['destinationBranchId'] as String,
      destinationBranchName: json['destinationBranchName'] as String,
      status: BranchTransferStatus.values[json['status'] as int],
      lines:
          (json['lines'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(BranchTransferLine.fromJson)
              .toList(),
    );
  }

  final String id;
  final String sourceBranchId;
  final String sourceBranchName;
  final String destinationBranchId;
  final String destinationBranchName;
  final BranchTransferStatus status;
  final List<BranchTransferLine> lines;
}

/// Mirrors Purch.Application.Inventory.BranchTransferLineDto.
class BranchTransferLine {
  const BranchTransferLine({
    required this.id,
    required this.itemId,
    required this.itemName,
    required this.quantity,
  });

  factory BranchTransferLine.fromJson(Map<String, dynamic> json) {
    return BranchTransferLine(
      id: json['id'] as String,
      itemId: json['itemId'] as String,
      itemName: json['itemName'] as String,
      quantity: (json['quantity'] as num).toDouble(),
    );
  }

  final String id;
  final String itemId;
  final String itemName;
  final double quantity;
}

/// Mirrors Purch.Application.Inventory.CreateBranchTransferRequest.
class CreateBranchTransferRequest {
  const CreateBranchTransferRequest({
    required this.sourceBranchId,
    required this.destinationBranchId,
    required this.lines,
  });

  final String sourceBranchId;
  final String destinationBranchId;
  final List<CreateBranchTransferLineRequest> lines;

  Map<String, dynamic> toJson() => {
    'sourceBranchId': sourceBranchId,
    'destinationBranchId': destinationBranchId,
    'lines': lines.map((line) => line.toJson()).toList(),
  };
}

/// Mirrors Purch.Application.Inventory.CreateBranchTransferLineRequest.
class CreateBranchTransferLineRequest {
  const CreateBranchTransferLineRequest({
    required this.itemId,
    required this.quantity,
  });

  final String itemId;
  final double quantity;

  Map<String, dynamic> toJson() => {'itemId': itemId, 'quantity': quantity};
}
