/// Mirrors Purch.Domain.Enums.DeviceType exactly, in declared order (sent/
/// received as a plain integer, same as onboarding_enums.dart's enums).
enum DeviceType { register, kiosk, orderBoard, kitchenDisplay, warehouseOfficer }

/// Mirrors Purch.Application.Onboarding.DeviceDto.
class Device {
  const Device({
    required this.id,
    required this.branchId,
    required this.pairingCode,
    required this.deviceIdentifier,
    required this.deviceType,
    required this.lastSeenAt,
  });

  factory Device.fromJson(Map<String, dynamic> json) {
    return Device(
      id: json['id'] as String,
      branchId: json['branchId'] as String,
      pairingCode: json['pairingCode'] as String,
      deviceIdentifier: json['deviceIdentifier'] as String?,
      deviceType: DeviceType.values[json['deviceType'] as int? ?? 0],
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
  final DeviceType deviceType;
  final DateTime? lastSeenAt;
}

/// Mirrors Purch.Application.Onboarding.CreateDeviceRequest. [pairingPin] is
/// required for every [DeviceType] except [DeviceType.register].
class CreateDeviceRequest {
  const CreateDeviceRequest({
    required this.branchId,
    this.deviceIdentifier,
    this.deviceType = DeviceType.register,
    this.pairingPin,
  });

  final String branchId;
  final String? deviceIdentifier;
  final DeviceType deviceType;
  final String? pairingPin;

  Map<String, dynamic> toJson() => {
    'branchId': branchId,
    'deviceIdentifier': deviceIdentifier,
    'deviceType': deviceType.index,
    'pairingPin': pairingPin,
  };
}
