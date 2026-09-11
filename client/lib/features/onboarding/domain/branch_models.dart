import 'hardware_enums.dart';

/// Mirrors Purch.Application.Onboarding.BranchDto.
class Branch {
  const Branch({
    required this.id,
    required this.name,
    required this.address,
    required this.receiptPrinterProfile,
    required this.cashDrawerEnabled,
    required this.cashDrawerPolicy,
  });

  factory Branch.fromJson(Map<String, dynamic> json) {
    return Branch(
      id: json['id'] as String,
      name: json['name'] as String,
      address: json['address'] as String?,
      receiptPrinterProfile:
          ReceiptPrinterProfile.values[json['receiptPrinterProfile'] as int],
      cashDrawerEnabled: json['cashDrawerEnabled'] as bool,
      cashDrawerPolicy:
          CashDrawerPolicy.values[json['cashDrawerPolicy'] as int],
    );
  }

  final String id;
  final String name;
  final String? address;
  final ReceiptPrinterProfile receiptPrinterProfile;
  final bool cashDrawerEnabled;
  final CashDrawerPolicy cashDrawerPolicy;
}

/// Mirrors Purch.Application.Onboarding.CreateBranchRequest.
class CreateBranchRequest {
  const CreateBranchRequest({required this.name, this.address});

  final String name;
  final String? address;

  Map<String, dynamic> toJson() => {'name': name, 'address': address};
}

/// Mirrors Purch.Application.Onboarding.UpdateBranchHardwareSettingsRequest.
/// The backend rejects cashDrawerEnabled=true with receiptPrinterProfile=none
/// (a drawer has no connection of its own — it's triggered through the
/// printer), surfaced here as a ValidationFailure like any other 400.
class UpdateBranchHardwareSettingsRequest {
  const UpdateBranchHardwareSettingsRequest({
    required this.receiptPrinterProfile,
    required this.cashDrawerEnabled,
    required this.cashDrawerPolicy,
  });

  final ReceiptPrinterProfile receiptPrinterProfile;
  final bool cashDrawerEnabled;
  final CashDrawerPolicy cashDrawerPolicy;

  Map<String, dynamic> toJson() => {
    'receiptPrinterProfile': receiptPrinterProfile.index,
    'cashDrawerEnabled': cashDrawerEnabled,
    'cashDrawerPolicy': cashDrawerPolicy.index,
  };
}
