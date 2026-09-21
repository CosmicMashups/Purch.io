import 'package:drift/drift.dart' show Value;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/db/app_database.dart';
import '../../../../core/data/paging.dart';
import '../../../../core/db/db_providers.dart';
import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/onboarding_repository_impl.dart';
import '../../domain/audit_log_models.dart';
import '../../domain/bootstrap_models.dart';
import '../../domain/branch_models.dart';
import '../../domain/department_models.dart';
import '../../domain/device_models.dart';
import '../../domain/onboarding_enums.dart';
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

/// Drives editing a single branch's manual GCash QR (D5) settings.
@riverpod
class ManualGcashQrSettingsController
    extends _$ManualGcashQrSettingsController {
  @override
  FutureOr<void> build() {}

  Future<bool> updateSettings(
    String branchId,
    UpdateManualGcashQrSettingsRequest request,
  ) async {
    state = const AsyncLoading();
    final repository = ref.read(onboardingRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updateManualGcashQrSettings(branchId, request),
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

/// Loads and refreshes the tenant settings (A2 branding, A5 BIR, barcode
/// toggle, B7 credit ledger toggle).
@riverpod
class TenantSettingsNotifier extends _$TenantSettingsNotifier {
  @override
  Future<TenantSettings> build() async {
    final settings =
        await ref.watch(onboardingRepositoryProvider).getTenantSettings();
    await _cacheBranding(settings);
    return settings;
  }

  /// Mirrors the tenant's branding into the device-local cache, which is what
  /// core/theming/theme_builder.dart watches to build the live ThemeData. Best
  /// effort: a cache write failure must never fail a settings load or save.
  Future<void> _cacheBranding(TenantSettings settings) async {
    try {
      final db = ref.read(appDatabaseProvider);
      await db
          .into(db.cachedBranding)
          .insertOnConflictUpdate(
            CachedBrandingCompanion.insert(
              tenantId: settings.id,
              logoUrl: Value(settings.brandingLogoUrl),
              backgroundColorHex: Value(settings.brandingBackgroundColorHex),
              accentColorHex: Value(settings.brandingAccentColorHex),
              primaryTextColorHex: Value(settings.brandingPrimaryTextColorHex),
              secondaryTextColorHex: Value(
                settings.brandingSecondaryTextColorHex,
              ),
              fontFamily: Value(settings.brandingFontFamily),
              kioskPosterImageUrl: Value(settings.kioskPosterImageUrl),
              lastSyncedAt: DateTime.now(),
            ),
          );
    } catch (_) {
      // Offline/unavailable local DB — the app just keeps its current theme.
    }
  }

  Future<bool> updateBranding(UpdateBrandingRequest request) =>
      _update((repository) => repository.updateBranding(request));

  Future<bool> updateBirSettings(UpdateBirSettingsRequest request) =>
      _update((repository) => repository.updateBirSettings(request));

  Future<bool> updateBarcodeSetting(bool requiresBarcodePerItem) => _update(
    (repository) => repository.updateBarcodeSetting(requiresBarcodePerItem),
  );

  Future<bool> updateCreditLedgerSetting(bool creditLedgerEnabled) => _update(
    (repository) => repository.updateCreditLedgerSetting(creditLedgerEnabled),
  );

  Future<bool> updateInventoryTrackingSetting(
    bool useSeparateInventoryTracking,
  ) => _update(
    (repository) => repository.updateInventoryTrackingSetting(
      useSeparateInventoryTracking,
    ),
  );

  Future<bool> _update(
    Future<TenantSettings> Function(OnboardingRepository) action,
  ) async {
    final repository = ref.read(onboardingRepositoryProvider);

    state = const AsyncLoading();
    final next = await AsyncValue.guard(() => action(repository));
    state = next;

    if (next.valueOrNull case final settings?) {
      await _cacheBranding(settings);
    }

    return !next.hasError;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Whether this tenant's vertical wants a Dine In/Take Out fulfillment
/// choice on every sale — true for Restaurant and Cafe, false for every
/// other [BusinessType]. Defaults to false while settings are still loading
/// or unavailable, since showing the selector unprompted is the safer miss.
@riverpod
bool isDineInTakeOutVertical(Ref ref) {
  final businessType = ref
      .watch(tenantSettingsNotifierProvider)
      .valueOrNull
      ?.businessType;
  return businessType == BusinessType.restaurant ||
      businessType == BusinessType.cafe;
}

/// Loads the audit log viewer (A6). Filtering by staff/date/action type is
/// deferred until there's real audit data (Phase 4) to make filtering worth
/// building a UI for — the backend query endpoint already supports it.
@riverpod
class AuditLogList extends _$AuditLogList {
  @override
  Future<List<AuditLogEntry>> build() async {
    final page = await ref
        .watch(onboardingRepositoryProvider)
        .listAuditLogs(limit: kLogPageSize);
    _hasMore = page.length >= kLogPageSize;
    return page;
  }

  bool _hasMore = false;
  bool _loadingMore = false;

  /// True while an older page may exist beyond what is loaded.
  bool get hasMore => _hasMore;

  /// Appends the next-older page. No-op while a load is running or when exhausted.
  Future<void> loadMore() async {
    final current = state.valueOrNull;
    if (!_hasMore || _loadingMore || current == null || current.isEmpty) return;
    _loadingMore = true;
    try {
      final page = await ref
          .read(onboardingRepositoryProvider)
          .listAuditLogs(before: current.last.createdAt, limit: kLogPageSize);
      _hasMore = page.length >= kLogPageSize;
      state = AsyncData([...current, ...page]);
    } finally {
      _loadingMore = false;
    }
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

/// One list per branch (Riverpod family, inferred from the `branchId`
/// parameter) — B6's departments/concessionaires.
@riverpod
class DepartmentList extends _$DepartmentList {
  @override
  Future<List<Department>> build(String branchId) {
    return ref.watch(onboardingRepositoryProvider).listDepartments(branchId);
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateDepartmentController extends _$CreateDepartmentController {
  @override
  FutureOr<void> build(String branchId) {}

  Future<bool> create(CreateDepartmentRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(onboardingRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.createDepartment(branchId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(departmentListProvider(branchId).notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
