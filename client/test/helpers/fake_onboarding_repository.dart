import 'package:purch_client/features/onboarding/domain/audit_log_models.dart';
import 'package:purch_client/features/onboarding/domain/bootstrap_models.dart';
import 'package:purch_client/features/onboarding/domain/branch_models.dart';
import 'package:purch_client/features/onboarding/domain/department_models.dart';
import 'package:purch_client/features/onboarding/domain/device_models.dart';
import 'package:purch_client/features/onboarding/domain/hardware_enums.dart';
import 'package:purch_client/features/onboarding/domain/onboarding_enums.dart';
import 'package:purch_client/features/onboarding/domain/onboarding_repository.dart';
import 'package:purch_client/features/onboarding/domain/staff_models.dart';
import 'package:purch_client/features/onboarding/domain/tenant_settings_models.dart';

class FakeOnboardingRepository implements OnboardingRepository {
  FakeOnboardingRepository({
    this.bootstrapFailure,
    this.createStaffFailure,
    this.createBranchFailure,
    this.createDeviceFailure,
    this.createDepartmentFailure,
    this.updateManualGcashQrSettingsFailure,
    List<StaffMember>? initialStaff,
    List<Branch>? initialBranches,
    List<Device>? initialDevices,
    TenantSettings? initialSettings,
    List<AuditLogEntry>? initialAuditLogs,
    Map<String, List<Department>>? initialDepartments,
  }) : staff = initialStaff ?? [],
       branches = initialBranches ?? [],
       devices = initialDevices ?? [],
       settings = initialSettings ?? _defaultSettings,
       auditLogs = initialAuditLogs ?? [],
       departmentsByBranch = initialDepartments ?? {};

  static const _defaultSettings = TenantSettings(
    id: 'tenant-1',
    name: 'Test Tenant',
    businessType: BusinessType.convenienceStore,
    brandingLogoUrl: null,
    brandingBackgroundColorHex: null,
    brandingAccentColorHex: null,
    brandingPrimaryTextColorHex: null,
    brandingSecondaryTextColorHex: null,
    brandingFontFamily: null,
    requiresBarcodePerItem: false,
    tin: null,
    registeredBusinessName: null,
    registeredAddress: null,
    creditLedgerRetentionDays: null,
    creditLedgerEnabled: false,
    kioskPosterImageUrl: null,
  );

  final Object? bootstrapFailure;
  final Object? createStaffFailure;
  final Object? createBranchFailure;
  final Object? createDeviceFailure;
  final Object? createDepartmentFailure;
  final Object? updateManualGcashQrSettingsFailure;
  final List<StaffMember> staff;
  final List<Branch> branches;
  final List<Device> devices;
  TenantSettings settings;
  final List<AuditLogEntry> auditLogs;
  final Map<String, List<Department>> departmentsByBranch;

  BootstrapRequest? lastBootstrapRequest;
  CreateStaffRequest? lastCreateStaffRequest;
  CreateBranchRequest? lastCreateBranchRequest;
  CreateDeviceRequest? lastCreateDeviceRequest;

  @override
  Future<BootstrapResult> bootstrap(BootstrapRequest request) async {
    lastBootstrapRequest = request;
    if (bootstrapFailure != null) {
      throw bootstrapFailure!;
    }
    return const BootstrapResult(
      tenantId: 'tenant-1',
      branchId: 'branch-1',
      deviceId: 'device-1',
      devicePairingCode: 'ABCD1234',
      adminUserId: 'admin-1',
    );
  }

  @override
  Future<List<StaffMember>> listStaff() async => staff;

  @override
  Future<StaffMember> createStaff(CreateStaffRequest request) async {
    lastCreateStaffRequest = request;
    if (createStaffFailure != null) {
      throw createStaffFailure!;
    }
    final created = StaffMember(
      id: 'staff-${staff.length + 1}',
      name: request.name,
      role: request.role,
      scopeType: ScopeType.tenant,
      scopeId: null,
      branchId: null,
      isActive: true,
    );
    staff.add(created);
    return created;
  }

  @override
  Future<StaffMember> updateStaff(
    String staffId,
    UpdateStaffRequest request,
  ) async {
    throw UnimplementedError();
  }

  @override
  Future<List<Branch>> listBranches() async => branches;

  @override
  Future<Branch> createBranch(CreateBranchRequest request) async {
    lastCreateBranchRequest = request;
    if (createBranchFailure != null) {
      throw createBranchFailure!;
    }
    final created = Branch(
      id: 'branch-${branches.length + 1}',
      name: request.name,
      address: request.address,
      receiptPrinterProfile: ReceiptPrinterProfile.none,
      cashDrawerEnabled: false,
      cashDrawerPolicy: CashDrawerPolicy.kickOnSaleOnly,
      manualGcashQrImageUrl: null,
      manualGcashAccountName: null,
      manualGcashAccountNumber: null,
    );
    branches.add(created);
    return created;
  }

  @override
  Future<Branch> updateBranchHardwareSettings(
    String branchId,
    UpdateBranchHardwareSettingsRequest request,
  ) async {
    throw UnimplementedError();
  }

  @override
  Future<Branch> updateManualGcashQrSettings(
    String branchId,
    UpdateManualGcashQrSettingsRequest request,
  ) async {
    if (updateManualGcashQrSettingsFailure != null) {
      throw updateManualGcashQrSettingsFailure!;
    }
    final index = branches.indexWhere((branch) => branch.id == branchId);
    final current = branches[index];
    final updated = Branch(
      id: current.id,
      name: current.name,
      address: current.address,
      receiptPrinterProfile: current.receiptPrinterProfile,
      cashDrawerEnabled: current.cashDrawerEnabled,
      cashDrawerPolicy: current.cashDrawerPolicy,
      manualGcashQrImageUrl: request.qrImageUrl,
      manualGcashAccountName: request.accountName,
      manualGcashAccountNumber: request.accountNumber,
    );
    branches[index] = updated;
    return updated;
  }

  @override
  Future<List<Device>> listDevices() async => devices;

  @override
  Future<Device> createDevice(CreateDeviceRequest request) async {
    lastCreateDeviceRequest = request;
    if (createDeviceFailure != null) {
      throw createDeviceFailure!;
    }
    final created = Device(
      id: 'device-${devices.length + 1}',
      branchId: request.branchId,
      pairingCode: 'CODE${devices.length + 1}',
      deviceIdentifier: request.deviceIdentifier,
      deviceType: request.deviceType,
      lastSeenAt: null,
    );
    devices.add(created);
    return created;
  }

  @override
  Future<TenantSettings> getTenantSettings() async => settings;

  @override
  Future<TenantSettings> updateBranding(UpdateBrandingRequest request) async {
    settings = TenantSettings(
      id: settings.id,
      name: settings.name,
      businessType: settings.businessType,
      brandingLogoUrl: request.logoUrl,
      brandingBackgroundColorHex: request.backgroundColorHex,
      brandingAccentColorHex: request.accentColorHex,
      brandingPrimaryTextColorHex: request.primaryTextColorHex,
      brandingSecondaryTextColorHex: request.secondaryTextColorHex,
      brandingFontFamily: request.fontFamily,
      requiresBarcodePerItem: settings.requiresBarcodePerItem,
      tin: settings.tin,
      registeredBusinessName: settings.registeredBusinessName,
      registeredAddress: settings.registeredAddress,
      creditLedgerRetentionDays: settings.creditLedgerRetentionDays,
      creditLedgerEnabled: settings.creditLedgerEnabled,
      kioskPosterImageUrl: request.kioskPosterImageUrl,
    );
    return settings;
  }

  @override
  Future<TenantSettings> updateBirSettings(
    UpdateBirSettingsRequest request,
  ) async {
    settings = TenantSettings(
      id: settings.id,
      name: settings.name,
      businessType: settings.businessType,
      brandingLogoUrl: settings.brandingLogoUrl,
      brandingBackgroundColorHex: settings.brandingBackgroundColorHex,
      brandingAccentColorHex: settings.brandingAccentColorHex,
      brandingPrimaryTextColorHex: settings.brandingPrimaryTextColorHex,
      brandingSecondaryTextColorHex: settings.brandingSecondaryTextColorHex,
      brandingFontFamily: settings.brandingFontFamily,
      requiresBarcodePerItem: settings.requiresBarcodePerItem,
      tin: request.tin,
      registeredBusinessName: request.registeredBusinessName,
      registeredAddress: request.registeredAddress,
      creditLedgerRetentionDays: request.creditLedgerRetentionDays,
      creditLedgerEnabled: settings.creditLedgerEnabled,
      kioskPosterImageUrl: settings.kioskPosterImageUrl,
    );
    return settings;
  }

  @override
  Future<TenantSettings> updateBarcodeSetting(
    bool requiresBarcodePerItem,
  ) async {
    settings = TenantSettings(
      id: settings.id,
      name: settings.name,
      businessType: settings.businessType,
      brandingLogoUrl: settings.brandingLogoUrl,
      brandingBackgroundColorHex: settings.brandingBackgroundColorHex,
      brandingAccentColorHex: settings.brandingAccentColorHex,
      brandingPrimaryTextColorHex: settings.brandingPrimaryTextColorHex,
      brandingSecondaryTextColorHex: settings.brandingSecondaryTextColorHex,
      brandingFontFamily: settings.brandingFontFamily,
      requiresBarcodePerItem: requiresBarcodePerItem,
      tin: settings.tin,
      registeredBusinessName: settings.registeredBusinessName,
      registeredAddress: settings.registeredAddress,
      creditLedgerRetentionDays: settings.creditLedgerRetentionDays,
      creditLedgerEnabled: settings.creditLedgerEnabled,
      kioskPosterImageUrl: settings.kioskPosterImageUrl,
    );
    return settings;
  }

  @override
  Future<TenantSettings> updateCreditLedgerSetting(
    bool creditLedgerEnabled,
  ) async {
    settings = TenantSettings(
      id: settings.id,
      name: settings.name,
      businessType: settings.businessType,
      brandingLogoUrl: settings.brandingLogoUrl,
      brandingBackgroundColorHex: settings.brandingBackgroundColorHex,
      brandingAccentColorHex: settings.brandingAccentColorHex,
      brandingPrimaryTextColorHex: settings.brandingPrimaryTextColorHex,
      brandingSecondaryTextColorHex: settings.brandingSecondaryTextColorHex,
      brandingFontFamily: settings.brandingFontFamily,
      requiresBarcodePerItem: settings.requiresBarcodePerItem,
      tin: settings.tin,
      registeredBusinessName: settings.registeredBusinessName,
      registeredAddress: settings.registeredAddress,
      creditLedgerRetentionDays: settings.creditLedgerRetentionDays,
      creditLedgerEnabled: creditLedgerEnabled,
      kioskPosterImageUrl: settings.kioskPosterImageUrl,
    );
    return settings;
  }

  @override
  Future<TenantSettings> updateInventoryTrackingSetting(
    bool useSeparateInventoryTracking,
  ) async {
    settings = TenantSettings(
      id: settings.id,
      name: settings.name,
      businessType: settings.businessType,
      brandingLogoUrl: settings.brandingLogoUrl,
      brandingBackgroundColorHex: settings.brandingBackgroundColorHex,
      brandingAccentColorHex: settings.brandingAccentColorHex,
      brandingPrimaryTextColorHex: settings.brandingPrimaryTextColorHex,
      brandingSecondaryTextColorHex: settings.brandingSecondaryTextColorHex,
      brandingFontFamily: settings.brandingFontFamily,
      requiresBarcodePerItem: settings.requiresBarcodePerItem,
      tin: settings.tin,
      registeredBusinessName: settings.registeredBusinessName,
      registeredAddress: settings.registeredAddress,
      creditLedgerRetentionDays: settings.creditLedgerRetentionDays,
      creditLedgerEnabled: settings.creditLedgerEnabled,
      kioskPosterImageUrl: settings.kioskPosterImageUrl,
      useSeparateInventoryTracking: useSeparateInventoryTracking,
    );
    return settings;
  }

  @override
  Future<List<AuditLogEntry>> listAuditLogs({
    DateTime? before,
    String? beforeId,
    int? limit,
  }) async {
    final page = auditLogs
        .where((e) {
          if (before == null) return true;
          if (e.createdAt.isBefore(before)) return true;
          if (e.createdAt.isAtSameMomentAs(before) && beforeId != null) {
            return e.id.compareTo(beforeId) < 0;
          }
          return false;
        })
        .toList()
      ..sort((a, b) {
        final c = b.createdAt.compareTo(a.createdAt);
        return c != 0 ? c : b.id.compareTo(a.id);
      });
    return limit == null ? page : page.take(limit).toList();
  }

  @override
  Future<List<Department>> listDepartments(String branchId) async =>
      departmentsByBranch[branchId] ?? [];

  @override
  Future<Department> createDepartment(
    String branchId,
    CreateDepartmentRequest request,
  ) async {
    if (createDepartmentFailure != null) {
      throw createDepartmentFailure!;
    }
    final list = departmentsByBranch.putIfAbsent(branchId, () => []);
    final created = Department(
      id: 'department-${list.length + 1}',
      branchId: branchId,
      name: request.name,
      concessionaireContactInfo: request.concessionaireContactInfo,
    );
    list.add(created);
    return created;
  }
}
