import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/onboarding_repository_impl.dart';
import '../../domain/bootstrap_models.dart';
import '../../domain/onboarding_repository.dart';
import '../../domain/staff_models.dart';

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
