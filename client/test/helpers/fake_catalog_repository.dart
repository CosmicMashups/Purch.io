import 'package:purch_client/features/catalog/domain/bundle_promo_rule_models.dart';
import 'package:purch_client/features/catalog/domain/catalog_repository.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_batch_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/item_variant_models.dart';
import 'package:purch_client/features/catalog/domain/modifier_models.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';

class FakeCatalogRepository implements CatalogRepository {
  FakeCatalogRepository({
    this.createCategoryFailure,
    this.createItemFailure,
    this.createModifierGroupFailure,
    this.addModifierFailure,
    this.receiveBatchFailure,
    this.createBundleRuleFailure,
    this.createVariantFailure,
    this.attachModifierGroupFailure,
    this.updateTingiConfigFailure,
    List<Category>? initialCategories,
    List<Item>? initialItems,
    List<ModifierGroup>? initialModifierGroups,
    List<ItemBatch>? initialBatches,
    List<BundlePromoRule>? initialBundleRules,
    List<ItemVariant>? initialVariants,
    Map<String, List<ModifierGroup>>? initialItemModifierGroups,
  }) : categories = initialCategories ?? [],
       items = initialItems ?? [],
       modifierGroups = initialModifierGroups ?? [],
       batches = initialBatches ?? [],
       bundleRules = initialBundleRules ?? [],
       variants = initialVariants ?? [],
       itemModifierGroups = initialItemModifierGroups ?? {};

  final Object? createCategoryFailure;
  final Object? createItemFailure;
  final Object? createModifierGroupFailure;
  final Object? addModifierFailure;
  final Object? receiveBatchFailure;
  final Object? createBundleRuleFailure;
  final Object? createVariantFailure;
  final Object? attachModifierGroupFailure;
  final Object? updateTingiConfigFailure;
  final List<Category> categories;
  final List<Item> items;
  final List<ModifierGroup> modifierGroups;
  final List<ItemBatch> batches;
  final List<BundlePromoRule> bundleRules;
  final List<ItemVariant> variants;
  final Map<String, List<ModifierGroup>> itemModifierGroups;

  CreateItemRequest? lastCreateItemRequest;
  CreateItemBatchRequest? lastReceiveBatchRequest;

  @override
  Future<List<Category>> listCategories() async => categories;

  @override
  Future<Category> createCategory(CreateCategoryRequest request) async {
    if (createCategoryFailure != null) {
      throw createCategoryFailure!;
    }
    final created = Category(
      id: 'category-${categories.length + 1}',
      name: request.name,
      sortOrder: request.sortOrder,
    );
    categories.add(created);
    return created;
  }

  @override
  Future<List<Item>> listItems() async => items;

  @override
  Future<Item> createItem(CreateItemRequest request) async {
    lastCreateItemRequest = request;
    if (createItemFailure != null) {
      throw createItemFailure!;
    }
    final created = Item(
      id: 'item-${items.length + 1}',
      name: request.name,
      sku: request.sku,
      barcode: request.barcode,
      categoryId: request.categoryId,
      basePrice: request.basePrice,
      imageUrl: request.imageUrl,
      pricingType: request.pricingType,
      stockOnHand: 0,
      isActive: true,
      tingiMode: TingiMode.none,
      packagedSize: null,
      tingiIncrementStep: null,
      tingiAllowedSizes: const [],
    );
    items.add(created);
    return created;
  }

  @override
  Future<List<ModifierGroup>> listModifierGroups() async => modifierGroups;

  @override
  Future<ModifierGroup> createModifierGroup(
    CreateModifierGroupRequest request,
  ) async {
    if (createModifierGroupFailure != null) {
      throw createModifierGroupFailure!;
    }
    final created = ModifierGroup(
      id: 'group-${modifierGroups.length + 1}',
      name: request.name,
      allowMultipleSelection: request.allowMultipleSelection,
      modifiers: const [],
    );
    modifierGroups.add(created);
    return created;
  }

  @override
  Future<ModifierGroup> addModifier(
    String groupId,
    CreateItemModifierRequest request,
  ) async {
    if (addModifierFailure != null) {
      throw addModifierFailure!;
    }
    final index = modifierGroups.indexWhere((group) => group.id == groupId);
    final group = modifierGroups[index];
    final updated = ModifierGroup(
      id: group.id,
      name: group.name,
      allowMultipleSelection: group.allowMultipleSelection,
      modifiers: [
        ...group.modifiers,
        ItemModifierOption(
          id: 'modifier-${group.modifiers.length + 1}',
          name: request.name,
          priceDelta: request.priceDelta,
        ),
      ],
    );
    modifierGroups[index] = updated;
    return updated;
  }

  @override
  Future<List<ItemBatch>> listBatches(String itemId) async => batches;

  @override
  Future<ItemBatch> receiveBatch(
    String itemId,
    CreateItemBatchRequest request,
  ) async {
    lastReceiveBatchRequest = request;
    if (receiveBatchFailure != null) {
      throw receiveBatchFailure!;
    }
    final created = ItemBatch(
      id: 'batch-${batches.length + 1}',
      lotNumber: request.lotNumber,
      expiryDate: request.expiryDate,
      quantityReceived: request.quantityReceived,
      quantityRemaining: request.quantityReceived,
      receivedAt: DateTime.now(),
    );
    batches.add(created);
    return created;
  }

  @override
  Future<List<BundlePromoRule>> listBundleRules(String itemId) async =>
      bundleRules;

  @override
  Future<BundlePromoRule> createBundleRule(
    String itemId,
    CreateBundlePromoRuleRequest request,
  ) async {
    if (createBundleRuleFailure != null) {
      throw createBundleRuleFailure!;
    }
    final created = BundlePromoRule(
      id: 'bundle-rule-${bundleRules.length + 1}',
      description: request.description,
      triggerQuantity: request.triggerQuantity,
      bundlePrice: request.bundlePrice,
      isActive: true,
    );
    bundleRules.add(created);
    return created;
  }

  @override
  Future<List<ItemVariant>> listVariants(String itemId) async => variants;

  @override
  Future<ItemVariant> createVariant(
    String itemId,
    CreateItemVariantRequest request,
  ) async {
    if (createVariantFailure != null) {
      throw createVariantFailure!;
    }
    final created = ItemVariant(
      id: 'variant-${variants.length + 1}',
      attributes: request.attributes,
      sku: request.sku,
      stockOnHand: 0,
      priceOverride: request.priceOverride,
      imageUrl: request.imageUrl,
    );
    variants.add(created);
    return created;
  }

  @override
  Future<List<ModifierGroup>> listModifierGroupsForItem(String itemId) async =>
      itemModifierGroups[itemId] ?? [];

  @override
  Future<ModifierGroup> attachModifierGroup(
    String itemId,
    AttachModifierGroupRequest request,
  ) async {
    if (attachModifierGroupFailure != null) {
      throw attachModifierGroupFailure!;
    }
    final group = modifierGroups.firstWhere(
      (group) => group.id == request.modifierGroupId,
    );
    final attached = itemModifierGroups.putIfAbsent(itemId, () => []);
    attached.add(group);
    return group;
  }

  @override
  Future<Item> updateTingiConfig(
    String itemId,
    UpdateTingiConfigRequest request,
  ) async {
    if (updateTingiConfigFailure != null) {
      throw updateTingiConfigFailure!;
    }
    final index = items.indexWhere((item) => item.id == itemId);
    final current = items[index];
    final updated = Item(
      id: current.id,
      name: current.name,
      sku: current.sku,
      barcode: current.barcode,
      categoryId: current.categoryId,
      basePrice: current.basePrice,
      imageUrl: current.imageUrl,
      pricingType: current.pricingType,
      stockOnHand: current.stockOnHand,
      isActive: current.isActive,
      tingiMode: request.tingiMode,
      packagedSize: request.packagedSize,
      tingiIncrementStep: request.tingiIncrementStep,
      tingiAllowedSizes: request.allowedSizes ?? const [],
    );
    items[index] = updated;
    return updated;
  }
}
