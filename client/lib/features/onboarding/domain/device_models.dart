/// Mirrors Purch.Domain.Enums.DeviceType exactly, in declared order (sent/
/// received as a plain integer, same as onboarding_enums.dart's enums).
enum DeviceType {
  register,
  kiosk,
  orderBoard,
  kitchenDisplay,
  warehouseOfficer,
  customerDisplay,
}

/// Mirrors Purch.Domain.Enums.DeviceStatus.
enum DeviceStatus { active, pending, revoked }

/// Mirrors Purch.Application.Onboarding.DeviceDto.
class Device {
  const Device({
    required this.id,
    required this.branchId,
    required this.deviceIdentifier,
    required this.deviceType,
    required this.lastSeenAt,
    this.name,
    this.status = DeviceStatus.active,
    this.pairedAt,
    this.pairingCodeExpiresAt,
    this.linkedRegisterDeviceId,
  });

  factory Device.fromJson(Map<String, dynamic> json) {
    DateTime? date(String key) =>
        json[key] == null ? null : DateTime.parse(json[key] as String);

    return Device(
      id: json['id'] as String,
      branchId: json['branchId'] as String,
      deviceIdentifier: json['deviceIdentifier'] as String?,
      deviceType: DeviceType.values[json['deviceType'] as int? ?? 0],
      lastSeenAt: date('lastSeenAt'),
      name: json['name'] as String?,
      status: DeviceStatus.values[json['status'] as int? ?? 0],
      pairedAt: date('pairedAt'),
      pairingCodeExpiresAt: date('pairingCodeExpiresAt'),
      linkedRegisterDeviceId: json['linkedRegisterDeviceId'] as String?,
    );
  }

  final String id;
  final String branchId;
  final String? deviceIdentifier;
  final DeviceType deviceType;
  final DateTime? lastSeenAt;

  /// What the admin called it when making the pairing code.
  final String? name;
  final DeviceStatus status;
  final DateTime? pairedAt;
  final DateTime? pairingCodeExpiresAt;

  /// For a Customer Display: the Register whose sale it shows.
  final String? linkedRegisterDeviceId;

  String get displayName => name ?? deviceIdentifier ?? 'Unlabeled device';
}

/// Mirrors Purch.Application.Devices.CreateDevicePairingRequest. A Customer
/// Display must name the Register it shows ([linkedRegisterDeviceId]).
class CreateDeviceRequest {
  const CreateDeviceRequest({
    required this.name,
    required this.branchId,
    this.deviceType = DeviceType.register,
    this.linkedRegisterDeviceId,
  });

  final String name;
  final String branchId;
  final DeviceType deviceType;
  final String? linkedRegisterDeviceId;

  Map<String, dynamic> toJson() => {
    'name': name,
    'deviceType': deviceType.index,
    'branchId': branchId,
    'linkedRegisterDeviceId': linkedRegisterDeviceId,
  };
}

/// The one-time code for a device, shown to the admin once; the server keeps
/// only a hash of it.
class DevicePairingCode {
  const DevicePairingCode({
    required this.device,
    required this.pairingCode,
    required this.expiresAt,
  });

  factory DevicePairingCode.fromJson(Map<String, dynamic> json) {
    return DevicePairingCode(
      device: Device.fromJson(json['device'] as Map<String, dynamic>),
      pairingCode: json['pairingCode'] as String,
      expiresAt: DateTime.parse(json['expiresAt'] as String),
    );
  }

  final Device device;
  final String pairingCode;
  final DateTime expiresAt;
}
