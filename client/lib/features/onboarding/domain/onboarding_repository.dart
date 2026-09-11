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

  Future<StaffMember> createStaff(CreateStaffRequest request);

  Future<StaffMember> updateStaff(String staffId, UpdateStaffRequest request);

  Future<List<Branch>> listBranches();

  Future<Branch> createBranch(CreateBranchRequest request);

  Future<Branch> updateBranchHardwareSettings(
    String branchId,
    UpdateBranchHardwareSettingsRequest request,
  );

  Future<List<Device>> listDevices();

  Future<Device> createDevice(CreateDeviceRequest request);

  Future<TenantSettings> getTenantSettings();

  Future<TenantSettings> updateBranding(UpdateBrandingRequest request);

  Future<TenantSettings> updateBirSettings(UpdateBirSettingsRequest request);

  Future<TenantSettings> updateBarcodeSetting(bool requiresBarcodePerItem);

  Future<TenantSettings> updateCreditLedgerSetting(bool creditLedgerEnabled);

  Future<List<AuditLogEntry>> listAuditLogs();

  Future<List<Department>> listDepartments(String branchId);

  Future<Department> createDepartment(
    String branchId,
    CreateDepartmentRequest request,
  );
}
