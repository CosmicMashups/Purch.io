import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/promo_code_repository_impl.dart';
import '../../domain/promo_code_models.dart';
import '../../domain/promo_code_repository.dart';

part 'promo_code_providers.g.dart';

@Riverpod(keepAlive: true)
PromoCodeRepository promoCodeRepository(Ref ref) {
  return PromoCodeRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

@riverpod
class PromoCodeList extends _$PromoCodeList {
  @override
  Future<List<PromoCode>> build() {
    return ref.watch(promoCodeRepositoryProvider).listPromoCodes();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreatePromoCodeController extends _$CreatePromoCodeController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreatePromoCodeRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(promoCodeRepositoryProvider);

    state = await AsyncValue.guard(() => repository.createPromoCode(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(promoCodeListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
