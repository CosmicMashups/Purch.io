/// Mirrors Purch.Application.Catalog.ItemBatchDto.
class ItemBatch {
  const ItemBatch({
    required this.id,
    required this.lotNumber,
    required this.expiryDate,
    required this.quantityReceived,
    required this.quantityRemaining,
    required this.receivedAt,
  });

  factory ItemBatch.fromJson(Map<String, dynamic> json) {
    return ItemBatch(
      id: json['id'] as String,
      lotNumber: json['lotNumber'] as String,
      expiryDate:
          json['expiryDate'] == null
              ? null
              : DateTime.parse(json['expiryDate'] as String),
      quantityReceived: (json['quantityReceived'] as num).toDouble(),
      quantityRemaining: (json['quantityRemaining'] as num).toDouble(),
      receivedAt: DateTime.parse(json['receivedAt'] as String),
    );
  }

  final String id;
  final String lotNumber;
  final DateTime? expiryDate;
  final double quantityReceived;
  final double quantityRemaining;
  final DateTime receivedAt;
}

/// Mirrors Purch.Application.Catalog.CreateItemBatchRequest. Receiving a
/// batch is how weight/volume stock enters the system — see the backend's
/// own note on ItemBatchService.
class CreateItemBatchRequest {
  const CreateItemBatchRequest({
    required this.lotNumber,
    this.expiryDate,
    required this.quantityReceived,
  });

  final String lotNumber;
  final DateTime? expiryDate;
  final double quantityReceived;

  Map<String, dynamic> toJson() => {
    'lotNumber': lotNumber,
    // DateOnly on the backend — send just the date portion.
    'expiryDate':
        expiryDate == null
            ? null
            : '${expiryDate!.year.toString().padLeft(4, '0')}-'
                '${expiryDate!.month.toString().padLeft(2, '0')}-'
                '${expiryDate!.day.toString().padLeft(2, '0')}',
    'quantityReceived': quantityReceived,
  };
}
