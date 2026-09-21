import '../../../../core/data/data_refresh.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/db/db_providers.dart';
import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/catalog_repository_impl.dart';
import '../../domain/bundle_promo_rule_models.dart';
import '../../domain/catalog_repository.dart';
import '../../domain/category_models.dart';
import '../../domain/item_batch_models.dart';
import '../../domain/item_combo_component_models.dart';
import '../../domain/item_models.dart';
import '../../domain/item_variant_models.dart';
import '../../domain/modifier_models.dart';
import '../../../onboarding/domain/department_models.dart';
import '../../../onboarding/presentation/providers/onboarding_providers.dart';

part 'catalog_providers.g.dart';

@Riverpod(keepAlive: true)
CatalogRepository catalogRepository(Ref ref) {
  final identityDao = ref.watch(deviceIdentityDaoProvider);
  return CatalogRepositoryImpl(
    apiClient: ref.watch(apiClientProvider),
    cache: ref.watch(catalogCacheDaoProvider),
    tenantId: () async => (await identityDao.getIdentity())?.tenantId,
    // Loaded from inside other providers' build(), where Riverpod forbids writing state synchronously.
    onFreshness: (staleSince) => Future.microtask(
      () => ref.read(catalogStaleSinceProvider.notifier).set(staleSince),
    ),
  );
}

/// Null while the catalog on screen is confirmed current; otherwise when the server last confirmed it
/// (the app is showing its saved copy because the server could not be reached).
@Riverpod(keepAlive: true)
class CatalogStaleSince extends _$CatalogStaleSince {
  @override
  DateTime? build() => null;

  // ignore: use_setters_to_change_properties
  void set(DateTime? value) => state = value;
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
class UpdateCategoryController extends _$UpdateCategoryController {
  @override
  FutureOr<void> build() {}

  Future<bool> updateCategory(
    String categoryId,
    UpdateCategoryRequest request,
  ) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updateCategory(categoryId, request),
    );
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
      refreshStockAndSalesData(ref);
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
class UpdateItemController extends _$UpdateItemController {
  @override
  FutureOr<void> build() {}

  Future<bool> updateItem(String itemId, UpdateItemRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updateItem(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
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

/// One list per item (Riverpod family, inferred from the `itemId` parameter).
@riverpod
class BundleRuleList extends _$BundleRuleList {
  @override
  Future<List<BundlePromoRule>> build(String itemId) {
    return ref.watch(catalogRepositoryProvider).listBundleRules(itemId);
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateBundleRuleController extends _$CreateBundleRuleController {
  @override
  FutureOr<void> build(String itemId) {}

  Future<bool> create(CreateBundlePromoRuleRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.createBundleRule(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(bundleRuleListProvider(itemId).notifier).refresh();
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
class ItemVariantList extends _$ItemVariantList {
  @override
  Future<List<ItemVariant>> build(String itemId) {
    return ref.watch(catalogRepositoryProvider).listVariants(itemId);
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateVariantController extends _$CreateVariantController {
  @override
  FutureOr<void> build(String itemId) {}

  Future<bool> create(CreateItemVariantRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.createVariant(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(itemVariantListProvider(itemId).notifier).refresh();
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

/// One list per item (Riverpod family, inferred from the `itemId` parameter)
/// — the modifier groups attached to that item, e.g. "Ice Level" on a drink.
@riverpod
class ItemModifierGroupList extends _$ItemModifierGroupList {
  @override
  Future<List<ModifierGroup>> build(String itemId) {
    return ref
        .watch(catalogRepositoryProvider)
        .listModifierGroupsForItem(itemId);
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class AttachModifierGroupController extends _$AttachModifierGroupController {
  @override
  FutureOr<void> build(String itemId) {}

  Future<bool> attach(AttachModifierGroupRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.attachModifierGroup(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(itemModifierGroupListProvider(itemId).notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

@riverpod
class UpdateTingiConfigController extends _$UpdateTingiConfigController {
  @override
  FutureOr<void> build(String itemId) {}

  Future<bool> updateTingiConfig(UpdateTingiConfigRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updateTingiConfig(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
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
class UpdateServiceDurationController
    extends _$UpdateServiceDurationController {
  @override
  FutureOr<void> build(String itemId) {}

  Future<bool> updateServiceDuration(
    UpdateServiceDurationRequest request,
  ) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updateServiceDuration(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
      await ref.read(itemListProvider.notifier).refresh();
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
class ItemComboComponentList extends _$ItemComboComponentList {
  @override
  Future<List<ItemComboComponent>> build(String itemId) {
    return ref.watch(catalogRepositoryProvider).listComboComponents(itemId);
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateComboComponentController extends _$CreateComboComponentController {
  @override
  FutureOr<void> build(String itemId) {}

  Future<bool> create(CreateItemComboComponentRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.createComboComponent(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(itemComboComponentListProvider(itemId).notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

@riverpod
class UpdateItemDepartmentController extends _$UpdateItemDepartmentController {
  @override
  FutureOr<void> build(String itemId) {}

  Future<bool> updateDepartment(UpdateItemDepartmentRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updateItemDepartment(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
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
class UpdateLowStockThresholdController
    extends _$UpdateLowStockThresholdController {
  @override
  FutureOr<void> build(String itemId) {}

  Future<bool> updateThreshold(UpdateLowStockThresholdRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(catalogRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.updateLowStockThreshold(itemId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
      await ref.read(itemListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Flattens departments across every branch for the item department picker —
/// most tenants have a single branch, so this keeps the picker simple rather
/// than requiring the cashier/admin to pick a branch first.
@riverpod
Future<List<Department>> allDepartments(Ref ref) async {
  final onboardingRepository = ref.watch(onboardingRepositoryProvider);
  final branches = await onboardingRepository.listBranches();

  final departments = <Department>[];
  for (final branch in branches) {
    departments.addAll(await onboardingRepository.listDepartments(branch.id));
  }
  return departments;
}
