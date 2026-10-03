import 'audit_log_models.dart';
import 'bootstrap_models.dart';
import 'branch_models.dart';
import 'department_models.dart';
import 'device_models.dart';
import 'staff_models.dart';
import 'tenant_settings_models.dart';

abstract class OnboardingRepository {
  Future<BootstrapResult> bootstrap(BootstrapRequest request);

  Future<List<StaffMember>> listStaff();

  /// Invites a person: makes a single-use link (no email is sent) that the admin hands over.
  Future<StaffInviteLink> createStaff(InviteStaffRequest request);

  Future<List<Branch>> listBranches();

  Future<Branch> createBranch(CreateBranchRequest request);

  Future<Branch> updateBranchHardwareSettings(
    String branchId,
    UpdateBranchHardwareSettingsRequest request,
  );

  Future<Branch> updateManualGcashQrSettings(
    String branchId,
    UpdateManualGcashQrSettingsRequest request,
  );

  Future<List<Device>> listDevices();

  /// Makes a device and its one-time pairing code.
  Future<DevicePairingCode> createDevice(CreateDeviceRequest request);

  /// A fresh one-time code for a device that is waiting to be paired.
  Future<DevicePairingCode> newPairingCode(String deviceId);

  Future<Device> revokeDevice(String deviceId);

  Future<TenantSettings> getTenantSettings();

  Future<TenantSettings> updateBranding(UpdateBrandingRequest request);

  Future<TenantSettings> updateBirSettings(UpdateBirSettingsRequest request);

  Future<TenantSettings> updateBarcodeSetting(bool requiresBarcodePerItem);

  Future<TenantSettings> updateCreditLedgerSetting(bool creditLedgerEnabled);

  Future<TenantSettings> updateInventoryTrackingSetting(
    bool useSeparateInventoryTracking,
  );

  /// Newest first. Pass the last entry's `createdAt` and `id` as [before] and [beforeId] for the next page.
  Future<List<AuditLogEntry>> listAuditLogs({DateTime? before, String? beforeId, int? limit});

  Future<List<Department>> listDepartments(String branchId);

  Future<Department> createDepartment(
    String branchId,
    CreateDepartmentRequest request,
  );
}
