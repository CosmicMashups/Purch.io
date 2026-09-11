import 'onboarding_enums.dart';

/// The one-time A1–A3 setup request — mirrors Purch.Application.Onboarding.BootstrapTenantRequest.
class BootstrapRequest {
  const BootstrapRequest({
    required this.tenantName,
    required this.businessType,
    required this.branchName,
    required this.adminName,
    required this.adminPin,
  });

  final String tenantName;
  final BusinessType businessType;
  final String branchName;
  final String adminName;
  final String adminPin;

  Map<String, dynamic> toJson() => {
    'tenantName': tenantName,
    'businessType': businessType.index,
    'branchName': branchName,
    'adminName': adminName,
    'adminPin': adminPin,
  };
}

/// Mirrors Purch.Application.Onboarding.BootstrapTenantResult. The admin logs
/// in normally afterward via POST /auth/login using devicePairingCode + the
/// PIN they just chose — bootstrap itself does not return a session token.
class BootstrapResult {
  const BootstrapResult({
    required this.tenantId,
    required this.branchId,
    required this.deviceId,
    required this.devicePairingCode,
    required this.adminUserId,
  });

  factory BootstrapResult.fromJson(Map<String, dynamic> json) {
    return BootstrapResult(
      tenantId: json['tenantId'] as String,
      branchId: json['branchId'] as String,
      deviceId: json['deviceId'] as String,
      devicePairingCode: json['devicePairingCode'] as String,
      adminUserId: json['adminUserId'] as String,
    );
  }

  final String tenantId;
  final String branchId;
  final String deviceId;
  final String devicePairingCode;
  final String adminUserId;
}
