/// The ways a supplier contact can be reached, as the API spells them.
const List<String> supplierContactModes = [
  'Call',
  'Viber',
  'Email',
  'Facebook',
  'Messenger',
  'Landline',
  'Others',
];

/// Mirrors Purch.Application.Inventory.SupplierContactDto.
class SupplierContact {
  const SupplierContact({
    required this.contactPerson,
    this.modes = const [],
    this.numbers = const [],
    this.emails = const [],
  });

  factory SupplierContact.fromJson(Map<String, dynamic> json) {
    return SupplierContact(
      contactPerson: json['contactPerson'] as String? ?? '',
      modes: (json['modes'] as List<dynamic>? ?? []).cast<String>(),
      numbers: (json['numbers'] as List<dynamic>? ?? []).cast<String>(),
      emails: (json['emails'] as List<dynamic>? ?? []).cast<String>(),
    );
  }

  final String contactPerson;
  final List<String> modes;
  final List<String> numbers;
  final List<String> emails;

  Map<String, dynamic> toJson() => {
    'contactPerson': contactPerson,
    'modes': modes,
    'numbers': numbers,
    'emails': emails,
  };
}

/// Mirrors Purch.Application.Inventory.SupplierDto.
class Supplier {
  const Supplier({
    required this.id,
    required this.name,
    required this.contactInfo,
    required this.isActive,
    this.specialization,
    this.address,
    this.tin,
    this.remarks,
    this.contacts = const [],
  });

  factory Supplier.fromJson(Map<String, dynamic> json) {
    return Supplier(
      id: json['id'] as String,
      name: json['name'] as String,
      contactInfo: json['contactInfo'] as String?,
      isActive: json['isActive'] as bool,
      specialization: json['specialization'] as String?,
      address: json['address'] as String?,
      tin: json['tin'] as String?,
      remarks: json['remarks'] as String?,
      contacts:
          (json['contacts'] as List<dynamic>? ?? [])
              .cast<Map<String, dynamic>>()
              .map(SupplierContact.fromJson)
              .toList(),
    );
  }

  final String id;
  final String name;

  /// A one-line summary of the contacts, or the old free text on suppliers
  /// saved before contacts were structured.
  final String? contactInfo;
  final bool isActive;
  final String? specialization;
  final String? address;
  final String? tin;
  final String? remarks;
  final List<SupplierContact> contacts;
}

/// Mirrors Purch.Application.Inventory.CreateSupplierRequest.
class CreateSupplierRequest {
  const CreateSupplierRequest({
    required this.name,
    this.specialization,
    this.address,
    this.tin,
    this.remarks,
    this.contacts = const [],
  });

  final String name;
  final String? specialization;
  final String? address;
  final String? tin;
  final String? remarks;
  final List<SupplierContact> contacts;

  Map<String, dynamic> toJson() => {
    'name': name,
    'specialization': specialization,
    'address': address,
    'tin': tin,
    'remarks': remarks,
    'contacts': contacts.map((c) => c.toJson()).toList(),
  };
}

/// Mirrors Purch.Application.Inventory.UpdateSupplierRequest.
class UpdateSupplierRequest extends CreateSupplierRequest {
  const UpdateSupplierRequest({
    required super.name,
    super.specialization,
    super.address,
    super.tin,
    super.remarks,
    super.contacts,
    this.isActive = true,
  });

  final bool isActive;

  @override
  Map<String, dynamic> toJson() => {...super.toJson(), 'isActive': isActive};
}
