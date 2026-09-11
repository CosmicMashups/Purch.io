/// Mirrors Purch.Application.Inventory.SupplierDto.
class Supplier {
  const Supplier({
    required this.id,
    required this.name,
    required this.contactInfo,
    required this.isActive,
  });

  factory Supplier.fromJson(Map<String, dynamic> json) {
    return Supplier(
      id: json['id'] as String,
      name: json['name'] as String,
      contactInfo: json['contactInfo'] as String?,
      isActive: json['isActive'] as bool,
    );
  }

  final String id;
  final String name;
  final String? contactInfo;
  final bool isActive;
}

/// Mirrors Purch.Application.Inventory.CreateSupplierRequest.
class CreateSupplierRequest {
  const CreateSupplierRequest({required this.name, this.contactInfo});

  final String name;
  final String? contactInfo;

  Map<String, dynamic> toJson() => {'name': name, 'contactInfo': contactInfo};
}
