import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/audit_log_models.dart';
import '../domain/bootstrap_models.dart';
import '../domain/branch_models.dart';
import '../domain/department_models.dart';
import '../domain/device_models.dart';
import '../domain/onboarding_repository.dart';
import '../domain/staff_models.dart';
import '../domain/tenant_settings_models.dart';

class OnboardingRepositoryImpl implements OnboardingRepository {
  OnboardingRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<BootstrapResult> bootstrap(BootstrapRequest request) {
    return _post(
      '/onboarding/bootstrap',
      request.toJson(),
      BootstrapResult.fromJson,
    );
  }

  @override
  Future<List<StaffMember>> listStaff() {
    return _getList('/staff', StaffMember.fromJson);
  }

  @override
  Future<StaffMember> createStaff(CreateStaffRequest request) {
    return _post('/staff', request.toJson(), StaffMember.fromJson);
  }

  @override
  Future<StaffMember> updateStaff(String staffId, UpdateStaffRequest request) {
    return _put('/staff/$staffId', request.toJson(), StaffMember.fromJson);
  }

  @override
  Future<List<Branch>> listBranches() {
    return _getList('/branches', Branch.fromJson);
  }

  @override
  Future<Branch> createBranch(CreateBranchRequest request) {
    return _post('/branches', request.toJson(), Branch.fromJson);
  }

  @override
  Future<Branch> updateBranchHardwareSettings(
    String branchId,
    UpdateBranchHardwareSettingsRequest request,
  ) {
    return _put(
      '/branches/$branchId/hardware-settings',
      request.toJson(),
      Branch.fromJson,
    );
  }

  @override
  Future<List<Device>> listDevices() {
    return _getList('/devices', Device.fromJson);
  }

  @override
  Future<Device> createDevice(CreateDeviceRequest request) {
    return _post('/devices', request.toJson(), Device.fromJson);
  }

  @override
  Future<TenantSettings> getTenantSettings() async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(
        '/tenant/settings',
      );
      return TenantSettings.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<TenantSettings> updateBranding(UpdateBrandingRequest request) {
    return _put(
      '/tenant/settings/branding',
      request.toJson(),
      TenantSettings.fromJson,
    );
  }

  @override
  Future<TenantSettings> updateBirSettings(UpdateBirSettingsRequest request) {
    return _put(
      '/tenant/settings/bir',
      request.toJson(),
      TenantSettings.fromJson,
    );
  }

  @override
  Future<TenantSettings> updateBarcodeSetting(bool requiresBarcodePerItem) {
    return _put('/tenant/settings/barcode', {
      'requiresBarcodePerItem': requiresBarcodePerItem,
    }, TenantSettings.fromJson);
  }

  @override
  Future<TenantSettings> updateCreditLedgerSetting(bool creditLedgerEnabled) {
    return _put('/tenant/settings/credit-ledger', {
      'creditLedgerEnabled': creditLedgerEnabled,
    }, TenantSettings.fromJson);
  }

  @override
  Future<List<AuditLogEntry>> listAuditLogs() {
    return _getList('/audit-logs', AuditLogEntry.fromJson);
  }

  @override
  Future<List<Department>> listDepartments(String branchId) {
    return _getList('/branches/$branchId/departments', Department.fromJson);
  }

  @override
  Future<Department> createDepartment(
    String branchId,
    CreateDepartmentRequest request,
  ) {
    return _post(
      '/branches/$branchId/departments',
      request.toJson(),
      Department.fromJson,
    );
  }

  Future<T> _post<T>(
    String path,
    Map<String, dynamic> data,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        path,
        data: data,
      );
      return fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  Future<T> _put<T>(
    String path,
    Map<String, dynamic> data,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.put<Map<String, dynamic>>(
        path,
        data: data,
      );
      return fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  Future<List<T>> _getList<T>(
    String path,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(path);
      return response.data!.cast<Map<String, dynamic>>().map(fromJson).toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
