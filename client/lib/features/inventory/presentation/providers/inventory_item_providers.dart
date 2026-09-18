import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../domain/inventory_item_models.dart';
import 'inventory_providers.dart';

part 'inventory_item_providers.g.dart';

/// Ingredient-level Inventory Items list — opt-in per tenant via
/// useSeparateInventoryTracking.
@riverpod
class InventoryItemList extends _$InventoryItemList {
  @override
  Future<List<InventoryItem>> build() {
    return ref.watch(inventoryRepositoryProvider).listInventoryItems();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateInventoryItemController extends _$CreateInventoryItemController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateInventoryItemRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(inventoryRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.createInventoryItem(request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(inventoryItemListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

@riverpod
class UpdateInventoryItemController extends _$UpdateInventoryItemController {
  @override
  FutureOr<void> build() {}

  Future<bool> updateItem(String id, UpdateInventoryItemRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(inventoryRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updateInventoryItem(id, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(inventoryItemListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

@riverpod
class PhysicalCountController extends _$PhysicalCountController {
  @override
  FutureOr<void> build() {}

  Future<bool> submit(String id, UpdatePhysicalCountRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(inventoryRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updatePhysicalCount(id, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(inventoryItemListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

@riverpod
class ReceiveInventoryStockController
    extends _$ReceiveInventoryStockController {
  @override
  FutureOr<void> build() {}

  Future<bool> submit(String id, ReceiveInventoryStockRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(inventoryRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.receiveInventoryStock(id, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(inventoryItemListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// One recipe per item (Riverpod family) — the recipe editor watches the
/// instance for the item it's editing.
@riverpod
class ItemRecipe extends _$ItemRecipe {
  @override
  Future<List<ItemRecipeLine>> build(String itemId) {
    return ref.watch(inventoryRepositoryProvider).getItemRecipe(itemId);
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class ReplaceItemRecipeController extends _$ReplaceItemRecipeController {
  @override
  FutureOr<void> build() {}

  Future<bool> replace(String itemId, ReplaceItemRecipeRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(inventoryRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.replaceItemRecipe(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(itemRecipeProvider(itemId).notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
