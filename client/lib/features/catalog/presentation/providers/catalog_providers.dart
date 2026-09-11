import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/catalog_repository_impl.dart';
import '../../domain/catalog_repository.dart';
import '../../domain/category_models.dart';
import '../../domain/item_models.dart';

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
