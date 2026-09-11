/// Mirrors Purch.Domain.Enums.ShiftStatus exactly, in declared order.
enum ShiftStatus { open, closed }

/// Mirrors Purch.Application.Shifts.ShiftDto — D7's cash-drawer session for
/// a device, one Open at a time.
class Shift {
  const Shift({
    required this.id,
    required this.branchId,
    required this.deviceId,
    required this.status,
    required this.openedByUserId,
    required this.openedByUserName,
    required this.openingCashAmount,
    required this.openedAt,
    required this.closedByUserId,
    required this.closedByUserName,
    required this.closingCashAmount,
    required this.expectedCashAmount,
    required this.varianceAmount,
    required this.handoverNotes,
    required this.approvedByUserId,
    required this.approvedByUserName,
    required this.closedAt,
  });

  factory Shift.fromJson(Map<String, dynamic> json) {
    return Shift(
      id: json['id'] as String,
      branchId: json['branchId'] as String,
      deviceId: json['deviceId'] as String,
      status: ShiftStatus.values[json['status'] as int],
      openedByUserId: json['openedByUserId'] as String,
      openedByUserName: json['openedByUserName'] as String,
      openingCashAmount: (json['openingCashAmount'] as num).toDouble(),
      openedAt: DateTime.parse(json['openedAt'] as String),
      closedByUserId: json['closedByUserId'] as String?,
      closedByUserName: json['closedByUserName'] as String?,
      closingCashAmount: (json['closingCashAmount'] as num?)?.toDouble(),
      expectedCashAmount: (json['expectedCashAmount'] as num?)?.toDouble(),
      varianceAmount: (json['varianceAmount'] as num?)?.toDouble(),
      handoverNotes: json['handoverNotes'] as String?,
      approvedByUserId: json['approvedByUserId'] as String?,
      approvedByUserName: json['approvedByUserName'] as String?,
      closedAt:
          json['closedAt'] == null
              ? null
              : DateTime.parse(json['closedAt'] as String),
    );
  }

  final String id;
  final String branchId;
  final String deviceId;
  final ShiftStatus status;
  final String openedByUserId;
  final String openedByUserName;
  final double openingCashAmount;
  final DateTime openedAt;
  final String? closedByUserId;
  final String? closedByUserName;
  final double? closingCashAmount;
  final double? expectedCashAmount;
  final double? varianceAmount;
  final String? handoverNotes;
  final String? approvedByUserId;
  final String? approvedByUserName;
  final DateTime? closedAt;
}

/// Mirrors Purch.Application.Shifts.OpenShiftRequest.
class OpenShiftRequest {
  const OpenShiftRequest({required this.openingCashAmount});

  final double openingCashAmount;

  Map<String, dynamic> toJson() => {'openingCashAmount': openingCashAmount};
}

/// Mirrors Purch.Application.Shifts.CloseShiftRequest. approverPin is only
/// needed when the closing count doesn't match the expected cash amount —
/// the backend rejects a mismatched close without it.
class CloseShiftRequest {
  const CloseShiftRequest({
    required this.closingCashAmount,
    this.handoverNotes,
    this.approverPin,
  });

  final double closingCashAmount;
  final String? handoverNotes;
  final String? approverPin;

  Map<String, dynamic> toJson() => {
    'closingCashAmount': closingCashAmount,
    'handoverNotes': handoverNotes,
    'approverPin': approverPin,
  };
}
