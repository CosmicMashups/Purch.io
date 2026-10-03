import 'onboarding_enums.dart';

/// The one-time A1–A3 setup request — mirrors Purch.Application.Onboarding.BootstrapTenantRequest.
class BootstrapRequest {
  const BootstrapRequest({
    required this.tenantName,
    required this.businessType,
    required this.branchName,
    required this.adminName,
    required this.adminPin,
    required this.adminEmail,
    required this.adminPassword,
  });

  final String tenantName;
  final BusinessType businessType;
  final String branchName;
  final String adminName;
  final String adminPin;
  final String adminEmail;
  final String adminPassword;

  Map<String, dynamic> toJson() => {
    'tenantName': tenantName,
    'businessType': businessType.index,
    'branchName': branchName,
    'adminName': adminName,
    'adminPin': adminPin,
    'adminEmail': adminEmail,
    'adminPassword': adminPassword,
  };
}

/// Mirrors Purch.Application.Onboarding.BootstrapTenantResult. The admin signs in
/// afterward with the email and password they just chose — bootstrap itself does
/// not return a session token. Devices are paired later from the Devices page.
class BootstrapResult {
  const BootstrapResult({
    required this.tenantId,
    required this.branchId,
    required this.adminMembershipId,
  });

  factory BootstrapResult.fromJson(Map<String, dynamic> json) {
    return BootstrapResult(
      tenantId: json['tenantId'] as String,
      branchId: json['branchId'] as String,
      adminMembershipId: json['adminMembershipId'] as String,
    );
  }

  final String tenantId;
  final String branchId;
  final String adminMembershipId;
}
