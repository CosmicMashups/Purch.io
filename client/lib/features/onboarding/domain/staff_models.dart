import 'onboarding_enums.dart';

/// Mirrors Purch.Application.Onboarding.StaffDto.
class StaffMember {
  const StaffMember({
    required this.id,
    required this.name,
    required this.role,
    required this.scopeType,
    required this.scopeId,
    required this.branchId,
    required this.isActive,
  });

  factory StaffMember.fromJson(Map<String, dynamic> json) {
    return StaffMember(
      id: json['id'] as String,
      name: json['name'] as String,
      role: StaffRole.values[json['role'] as int],
      scopeType: ScopeType.values[json['scopeType'] as int],
      scopeId: json['scopeId'] as String?,
      branchId: json['branchId'] as String?,
      isActive: json['isActive'] as bool,
    );
  }

  final String id;
  final String name;
  final StaffRole role;
  final ScopeType scopeType;
  final String? scopeId;
  final String? branchId;
  final bool isActive;
}

/// Mirrors Purch.Application.Onboarding.CreateStaffRequest. Branch-scoped
/// staff (ScopeType.branch + a chosen branch) is left to when the branch
/// management screen lands — this form only creates tenant-wide staff for now.
class CreateStaffRequest {
  const CreateStaffRequest({
    required this.name,
    required this.role,
    required this.pin,
  });

  final String name;
  final StaffRole role;
  final String pin;

  Map<String, dynamic> toJson() => {
    'name': name,
    'role': role.index,
    'scopeType': ScopeType.tenant.index,
    'scopeId': null,
    'branchId': null,
    'pin': pin,
  };
}

/// Mirrors Purch.Application.Onboarding.UpdateStaffRequest.
class UpdateStaffRequest {
  const UpdateStaffRequest({
    required this.role,
    required this.scopeType,
    required this.scopeId,
    required this.branchId,
    required this.isActive,
  });

  final StaffRole role;
  final ScopeType scopeType;
  final String? scopeId;
  final String? branchId;
  final bool isActive;

  Map<String, dynamic> toJson() => {
    'role': role.index,
    'scopeType': scopeType.index,
    'scopeId': scopeId,
    'branchId': branchId,
    'isActive': isActive,
  };
}
