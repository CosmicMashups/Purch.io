/// Mirrors Purch.Application.Onboarding.DeviceDto.
class Device {
  const Device({
    required this.id,
    required this.branchId,
    required this.pairingCode,
    required this.deviceIdentifier,
    required this.lastSeenAt,
  });

  factory Device.fromJson(Map<String, dynamic> json) {
    return Device(
      id: json['id'] as String,
      branchId: json['branchId'] as String,
      pairingCode: json['pairingCode'] as String,
      deviceIdentifier: json['deviceIdentifier'] as String?,
      lastSeenAt:
          json['lastSeenAt'] == null
              ? null
              : DateTime.parse(json['lastSeenAt'] as String),
    );
  }

  final String id;
  final String branchId;
  final String pairingCode;
  final String? deviceIdentifier;
  final DateTime? lastSeenAt;
}

/// Mirrors Purch.Application.Onboarding.CreateDeviceRequest.
class CreateDeviceRequest {
  const CreateDeviceRequest({required this.branchId, this.deviceIdentifier});

  final String branchId;
  final String? deviceIdentifier;

  Map<String, dynamic> toJson() => {
    'branchId': branchId,
    'deviceIdentifier': deviceIdentifier,
  };
}
