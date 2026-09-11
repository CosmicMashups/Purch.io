/// Mirrors Purch.Application.Onboarding.DepartmentDto — B6's concessionaire
/// mode: a department/stall within a branch (e.g. "Bakery Stall") that items
/// can be assigned to (see Item.departmentId in the catalog feature).
class Department {
  const Department({
    required this.id,
    required this.branchId,
    required this.name,
    required this.concessionaireContactInfo,
  });

  factory Department.fromJson(Map<String, dynamic> json) {
    return Department(
      id: json['id'] as String,
      branchId: json['branchId'] as String,
      name: json['name'] as String,
      concessionaireContactInfo: json['concessionaireContactInfo'] as String?,
    );
  }

  final String id;
  final String branchId;
  final String name;
  final String? concessionaireContactInfo;
}

/// Mirrors Purch.Application.Onboarding.CreateDepartmentRequest.
class CreateDepartmentRequest {
  const CreateDepartmentRequest({
    required this.name,
    this.concessionaireContactInfo,
  });

  final String name;
  final String? concessionaireContactInfo;

  Map<String, dynamic> toJson() => {
    'name': name,
    'concessionaireContactInfo': concessionaireContactInfo,
  };
}
