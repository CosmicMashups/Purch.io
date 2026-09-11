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
    required this.manualGcashQrImageUrl,
    required this.manualGcashAccountName,
    required this.manualGcashAccountNumber,
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
      manualGcashQrImageUrl: json['manualGcashQrImageUrl'] as String?,
      manualGcashAccountName: json['manualGcashAccountName'] as String?,
      manualGcashAccountNumber: json['manualGcashAccountNumber'] as String?,
    );
  }

  final String id;
  final String name;
  final String? address;
  final ReceiptPrinterProfile receiptPrinterProfile;
  final bool cashDrawerEnabled;
  final CashDrawerPolicy cashDrawerPolicy;

  /// D5 — a merchant-uploaded static QR Ph code (e.g. GCash's own "receive
  /// money" QR), paid directly into the tenant's own account with no gateway
  /// fee and no webhook confirmation. Independent of Xendit's dynamic QR Ph.
  final String? manualGcashQrImageUrl;
  final String? manualGcashAccountName;
  final String? manualGcashAccountNumber;
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

/// Mirrors Purch.Application.Onboarding.UpdateManualGcashQrSettingsRequest.
/// qrImageUrl is an already-hosted URL, not a file upload — same convention
/// as branding's logo. The backend rejects a QR image with no account name
/// or number to verify it against.
class UpdateManualGcashQrSettingsRequest {
  const UpdateManualGcashQrSettingsRequest({
    this.qrImageUrl,
    this.accountName,
    this.accountNumber,
  });

  final String? qrImageUrl;
  final String? accountName;
  final String? accountNumber;

  Map<String, dynamic> toJson() => {
    'qrImageUrl': qrImageUrl,
    'accountName': accountName,
    'accountNumber': accountNumber,
  };
}
