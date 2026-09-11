import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/bir_reading_repository_impl.dart';
import '../../domain/bir_reading_models.dart';
import '../../domain/bir_reading_repository.dart';

part 'bir_reading_providers.g.dart';

@Riverpod(keepAlive: true)
BirReadingRepository birReadingRepository(Ref ref) {
  return BirReadingRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

/// Holds the most recently generated reading, if any — generating is an
/// explicit action (unlike CartNotifier/CurrentShiftNotifier, there's
/// nothing to auto-fetch on first watch).
@riverpod
class GenerateBirReadingController extends _$GenerateBirReadingController {
  @override
  FutureOr<BirReading?> build() => null;

  Future<bool> generateXReading() =>
      _generate((repository) => repository.generateXReading());

  Future<bool> generateZReading() =>
      _generate((repository) => repository.generateZReading());

  Future<bool> _generate(
    Future<BirReading> Function(BirReadingRepository) action,
  ) async {
    final repository = ref.read(birReadingRepositoryProvider);

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
