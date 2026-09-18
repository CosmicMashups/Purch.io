import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/item_promo_repository_impl.dart';
import '../../domain/item_promo_models.dart';
import '../../domain/item_promo_repository.dart';

part 'item_promo_providers.g.dart';

@Riverpod(keepAlive: true)
ItemPromoRepository itemPromoRepository(Ref ref) {
  return ItemPromoRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

// --- BOGO ---------------------------------------------------------------

@riverpod
class BogoPromoRuleList extends _$BogoPromoRuleList {
  @override
  Future<List<BogoPromoRule>> build() {
    return ref.watch(itemPromoRepositoryProvider).listBogoPromoRules();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class BogoPromoRuleController extends _$BogoPromoRuleController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateBogoPromoRuleRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(itemPromoRepositoryProvider);
    state = await AsyncValue.guard(
      () => repository.createBogoPromoRule(request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(bogoPromoRuleListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Future<bool> edit(String id, UpdateBogoPromoRuleRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(itemPromoRepositoryProvider);
    state = await AsyncValue.guard(
      () => repository.updateBogoPromoRule(id, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(bogoPromoRuleListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

// --- Combo ----------------------------------------------------------------

@riverpod
class ComboPromoRuleList extends _$ComboPromoRuleList {
  @override
  Future<List<ComboPromoRule>> build() {
    return ref.watch(itemPromoRepositoryProvider).listComboPromoRules();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class ComboPromoRuleController extends _$ComboPromoRuleController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateComboPromoRuleRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(itemPromoRepositoryProvider);
    state = await AsyncValue.guard(
      () => repository.createComboPromoRule(request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(comboPromoRuleListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Future<bool> edit(String id, UpdateComboPromoRuleRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(itemPromoRepositoryProvider);
    state = await AsyncValue.guard(
      () => repository.updateComboPromoRule(id, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(comboPromoRuleListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

// --- Item discount ----------------------------------------------------

@riverpod
class ItemDiscountPromoRuleList extends _$ItemDiscountPromoRuleList {
  @override
  Future<List<ItemDiscountPromoRule>> build() {
    return ref.watch(itemPromoRepositoryProvider).listItemDiscountPromoRules();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class ItemDiscountPromoRuleController
    extends _$ItemDiscountPromoRuleController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateItemDiscountPromoRuleRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(itemPromoRepositoryProvider);
    state = await AsyncValue.guard(
      () => repository.createItemDiscountPromoRule(request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(itemDiscountPromoRuleListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Future<bool> edit(
    String id,
    UpdateItemDiscountPromoRuleRequest request,
  ) async {
    state = const AsyncLoading();
    final repository = ref.read(itemPromoRepositoryProvider);
    state = await AsyncValue.guard(
      () => repository.updateItemDiscountPromoRule(id, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(itemDiscountPromoRuleListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
