import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/catalog_repository_impl.dart';
import '../../domain/catalog_repository.dart';
import '../../domain/category_models.dart';
import '../../domain/item_batch_models.dart';
import '../../domain/item_models.dart';
import '../../domain/modifier_models.dart';

part 'catalog_providers.g.dart';

@Riverpod(keepAlive: true)
CatalogRepository catalogRepository(Ref ref) {
  return CatalogRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

@riverpod
class CategoryList extends _$CategoryList {
  @override
  Future<List<Category>> build() {
    return ref.watch(catalogRepositoryProvider).listCategories();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateCategoryController extends _$CreateCategoryController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateCategoryRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(() => repository.createCategory(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(categoryListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

@riverpod
class ItemList extends _$ItemList {
  @override
  Future<List<Item>> build() {
    return ref.watch(catalogRepositoryProvider).listItems();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateItemController extends _$CreateItemController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateItemRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(() => repository.createItem(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(itemListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

@riverpod
class ModifierGroupList extends _$ModifierGroupList {
  @override
  Future<List<ModifierGroup>> build() {
    return ref.watch(catalogRepositoryProvider).listModifierGroups();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateModifierGroupController extends _$CreateModifierGroupController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateModifierGroupRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.createModifierGroup(request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(modifierGroupListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// One controller instance per group being edited (Riverpod family, inferred
/// from the `groupId` parameter) — so adding a modifier to group A can't be
/// confused with an in-flight add on group B.
@riverpod
class AddModifierController extends _$AddModifierController {
  @override
  FutureOr<void> build(String groupId) {}

  Future<bool> add(CreateItemModifierRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.addModifier(groupId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(modifierGroupListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// One list per item (Riverpod family, inferred from the `itemId` parameter).
@riverpod
class ItemBatchList extends _$ItemBatchList {
  @override
  Future<List<ItemBatch>> build(String itemId) {
    return ref.watch(catalogRepositoryProvider).listBatches(itemId);
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class ReceiveBatchController extends _$ReceiveBatchController {
  @override
  FutureOr<void> build(String itemId) {}

  Future<bool> receive(CreateItemBatchRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.receiveBatch(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(itemBatchListProvider(itemId).notifier).refresh();
      await ref
          .read(itemListProvider.notifier)
          .refresh(); // StockOnHand changed too
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
