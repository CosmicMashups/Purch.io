import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/onboarding_repository_impl.dart';
import '../../domain/audit_log_models.dart';
import '../../domain/bootstrap_models.dart';
import '../../domain/branch_models.dart';
import '../../domain/device_models.dart';
import '../../domain/onboarding_repository.dart';
import '../../domain/staff_models.dart';
import '../../domain/tenant_settings_models.dart';

part 'onboarding_providers.g.dart';

@Riverpod(keepAlive: true)
OnboardingRepository onboardingRepository(Ref ref) {
  return OnboardingRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

/// Drives the bootstrap (tenant setup) screen.
@riverpod
class BootstrapController extends _$BootstrapController {
  @override
  FutureOr<BootstrapResult?> build() => null;

  Future<void> bootstrap(BootstrapRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(onboardingRepositoryProvider);
    state = await AsyncValue.guard(() => repository.bootstrap(request));
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Loads and refreshes the staff list (A4).
@riverpod
class StaffList extends _$StaffList {
  @override
  Future<List<StaffMember>> build() {
    return ref.watch(onboardingRepositoryProvider).listStaff();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

/// Drives the "add staff" form as its own in-flight state, separate from the
/// list itself — the list only needs to know when to refresh, not track the
/// creation request's loading/error state.
@riverpod
class CreateStaffController extends _$CreateStaffController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateStaffRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(onboardingRepositoryProvider);

    state = await AsyncValue.guard(() => repository.createStaff(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(staffListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Loads and refreshes the branch list (A3).
@riverpod
class BranchList extends _$BranchList {
  @override
  Future<List<Branch>> build() {
    return ref.watch(onboardingRepositoryProvider).listBranches();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateBranchController extends _$CreateBranchController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateBranchRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(onboardingRepositoryProvider);

    state = await AsyncValue.guard(() => repository.createBranch(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(branchListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Drives editing a single branch's hardware settings.
@riverpod
class BranchHardwareSettingsController
    extends _$BranchHardwareSettingsController {
  @override
  FutureOr<void> build() {}

  Future<bool> updateSettings(
    String branchId,
    UpdateBranchHardwareSettingsRequest request,
  ) async {
    state = const AsyncLoading();
    final repository = ref.read(onboardingRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updateBranchHardwareSettings(branchId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(branchListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Loads and refreshes the device list (A3).
@riverpod
class DeviceList extends _$DeviceList {
  @override
  Future<List<Device>> build() {
    return ref.watch(onboardingRepositoryProvider).listDevices();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateDeviceController extends _$CreateDeviceController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateDeviceRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(onboardingRepositoryProvider);

    state = await AsyncValue.guard(() => repository.createDevice(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(deviceListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Loads and refreshes the tenant settings (A2 branding, A5 BIR, barcode toggle).
@riverpod
class TenantSettingsNotifier extends _$TenantSettingsNotifier {
  @override
  Future<TenantSettings> build() {
    return ref.watch(onboardingRepositoryProvider).getTenantSettings();
  }

  Future<bool> updateBranding(UpdateBrandingRequest request) =>
      _update((repository) => repository.updateBranding(request));

  Future<bool> updateBirSettings(UpdateBirSettingsRequest request) =>
      _update((repository) => repository.updateBirSettings(request));

  Future<bool> updateBarcodeSetting(bool requiresBarcodePerItem) => _update(
    (repository) => repository.updateBarcodeSetting(requiresBarcodePerItem),
  );

  Future<bool> _update(
    Future<TenantSettings> Function(OnboardingRepository) action,
  ) async {
    final repository = ref.read(onboardingRepositoryProvider);

    state = const AsyncLoading();
    final next = await AsyncValue.guard(() => action(repository));
    state = next;

    return !next.hasError;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Loads the audit log viewer (A6). Filtering by staff/date/action type is
/// deferred until there's real audit data (Phase 4) to make filtering worth
/// building a UI for — the backend query endpoint already supports it.
@riverpod
class AuditLogList extends _$AuditLogList {
  @override
  Future<List<AuditLogEntry>> build() {
    return ref.watch(onboardingRepositoryProvider).listAuditLogs();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}
