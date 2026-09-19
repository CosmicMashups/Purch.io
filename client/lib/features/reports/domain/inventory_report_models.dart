import '../../inventory/domain/inventory_movement_models.dart';

/// Mirrors Purch.Application.Reporting.MovementSummaryDto (F3).
class MovementSummary {
  const MovementSummary({
    required this.from,
    required this.to,
    required this.byType,
  });

  factory MovementSummary.fromJson(Map<String, dynamic> json) {
    return MovementSummary(
      from: DateTime.parse(json['from'] as String),
      to: DateTime.parse(json['to'] as String),
      byType:
          (json['byType'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(MovementTypeSummary.fromJson)
              .toList(),
    );
  }

  final DateTime from;
  final DateTime to;
  final List<MovementTypeSummary> byType;
}

class MovementTypeSummary {
  const MovementTypeSummary({
    required this.type,
    required this.totalQuantity,
    required this.movementCount,
  });

  factory MovementTypeSummary.fromJson(Map<String, dynamic> json) {
    return MovementTypeSummary(
      type: MovementType.fromServer(json['type']),
      totalQuantity: (json['totalQuantity'] as num).toDouble(),
      movementCount: json['movementCount'] as int,
    );
  }

  final MovementType type;
  final double totalQuantity;
  final int movementCount;
}
