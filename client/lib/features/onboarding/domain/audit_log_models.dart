/// Sent/received as a plain integer — see onboarding_enums.dart for why.
/// Mirrors Purch.Domain.Enums.AuditActionType.
enum AuditActionType {
  void_,
  refund,
  discountOverride,
  priceOverride,
  inventoryAdjustment,
  departmentReassignment,
  creditLimitOverride,
  cashDrawerManualOpen,
}

/// Mirrors Purch.Application.Onboarding.AuditLogDto.
class AuditLogEntry {
  const AuditLogEntry({
    required this.id,
    required this.actorUserId,
    required this.actionType,
    required this.targetEntityType,
    required this.targetEntityId,
    required this.createdAt,
  });

  factory AuditLogEntry.fromJson(Map<String, dynamic> json) {
    return AuditLogEntry(
      id: json['id'] as String,
      actorUserId: json['actorUserId'] as String,
      actionType: AuditActionType.values[json['actionType'] as int],
      targetEntityType: json['targetEntityType'] as String,
      targetEntityId: json['targetEntityId'] as String,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }

  final String id;
  final String actorUserId;
  final AuditActionType actionType;
  final String targetEntityType;
  final String targetEntityId;
  final DateTime createdAt;
}
