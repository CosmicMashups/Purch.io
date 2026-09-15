import 'package:purch_client/features/catalog/domain/bundle_promo_rule_models.dart';
import 'package:purch_client/features/catalog/domain/catalog_repository.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_batch_models.dart';
import 'package:purch_client/features/catalog/domain/item_combo_component_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/item_variant_models.dart';
import 'package:purch_client/features/catalog/domain/modifier_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';

class FakeCatalogRepository implements CatalogRepository {
  FakeCatalogRepository({
    this.createCategoryFailure,
    this.updateCategoryFailure,
    this.createItemFailure,
    this.updateItemFailure,
    this.createModifierGroupFailure,
    this.addModifierFailure,
    this.receiveBatchFailure,
    this.createBundleRuleFailure,
    this.createVariantFailure,
    this.attachModifierGroupFailure,
    this.updateTingiConfigFailure,
    this.updateServiceDurationFailure,
    this.createComboComponentFailure,
    this.updateItemDepartmentFailure,
    this.updateLowStockThresholdFailure,
    List<Category>? initialCategories,
    List<Item>? initialItems,
    List<ModifierGroup>? initialModifierGroups,
    List<ItemBatch>? initialBatches,
    List<BundlePromoRule>? initialBundleRules,
    List<ItemVariant>? initialVariants,
    Map<String, List<ModifierGroup>>? initialItemModifierGroups,
    List<ItemComboComponent>? initialComboComponents,
  }) : categories = initialCategories ?? [],
       items = initialItems ?? [],
       modifierGroups = initialModifierGroups ?? [],
       batches = initialBatches ?? [],
       bundleRules = initialBundleRules ?? [],
       variants = initialVariants ?? [],
       itemModifierGroups = initialItemModifierGroups ?? {},
       comboComponents = initialComboComponents ?? [];

  final Object? createCategoryFailure;
  final Object? updateCategoryFailure;
  final Object? createItemFailure;
  final Object? updateItemFailure;
  final Object? createModifierGroupFailure;
  final Object? addModifierFailure;
  final Object? receiveBatchFailure;
  final Object? createBundleRuleFailure;
  final Object? createVariantFailure;
  final Object? attachModifierGroupFailure;
  final Object? updateTingiConfigFailure;
  final Object? updateServiceDurationFailure;
  final Object? createComboComponentFailure;
  final Object? updateItemDepartmentFailure;
  final Object? updateLowStockThresholdFailure;
  final List<Category> categories;
  final List<Item> items;
  final List<ModifierGroup> modifierGroups;
  final List<ItemBatch> batches;
  final List<BundlePromoRule> bundleRules;
  final List<ItemVariant> variants;
  final Map<String, List<ModifierGroup>> itemModifierGroups;
  final List<ItemComboComponent> comboComponents;

  CreateItemRequest? lastCreateItemRequest;
  UpdateItemRequest? lastUpdateItemRequest;
  CreateCategoryRequest? lastCreateCategoryRequest;
  UpdateCategoryRequest? lastUpdateCategoryRequest;
  CreateItemBatchRequest? lastReceiveBatchRequest;
  UpdateLowStockThresholdRequest? lastUpdateLowStockThresholdRequest;

  @override
  Future<List<Category>> listCategories() async => categories;

  @override
  Future<Category> createCategory(CreateCategoryRequest request) async {
    lastCreateCategoryRequest = request;
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
  Future<Category> updateCategory(
    String categoryId,
    UpdateCategoryRequest request,
  ) async {
    lastUpdateCategoryRequest = request;
    if (updateCategoryFailure != null) {
      throw updateCategoryFailure!;
    }
    final index = categories.indexWhere((c) => c.id == categoryId);
    final updated = Category(
      id: categoryId,
      name: request.name,
      sortOrder: request.sortOrder,
      imageUrl: request.imageUrl,
    );
    if (index >= 0) {
      categories[index] = updated;
    } else {
      categories.add(updated);
    }
    return updated;
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
      serviceDurationMinutes: null,
      departmentId: null,
      lowStockThreshold: null,
    );
    items.add(created);
    return created;
  }

  @override
  Future<Item> updateItem(String itemId, UpdateItemRequest request) async {
    lastUpdateItemRequest = request;
    if (updateItemFailure != null) {
      throw updateItemFailure!;
    }
    final index = items.indexWhere((i) => i.id == itemId);
    final existing = index >= 0 ? items[index] : null;
    final updated = Item(
      id: itemId,
      name: request.name,
      sku: request.sku,
      barcode: request.barcode,
      categoryId: request.categoryId,
      basePrice: request.basePrice,
      imageUrl: request.imageUrl,
      pricingType: existing?.pricingType ?? PricingType.unit,
      stockOnHand: existing?.stockOnHand ?? 0,
      isActive: request.isActive,
      tingiMode: existing?.tingiMode ?? TingiMode.none,
      packagedSize: existing?.packagedSize,
      tingiIncrementStep: existing?.tingiIncrementStep,
      tingiAllowedSizes: existing?.tingiAllowedSizes ?? const [],
      serviceDurationMinutes: existing?.serviceDurationMinutes,
      departmentId: request.departmentId,
      lowStockThreshold: existing?.lowStockThreshold,
    );
    if (index >= 0) {
      items[index] = updated;
    } else {
      items.add(updated);
    }
    return updated;
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
      isRequired: request.isRequired,
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
      isRequired: group.isRequired,
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
      serviceDurationMinutes: current.serviceDurationMinutes,
      departmentId: current.departmentId,
      lowStockThreshold: current.lowStockThreshold,
    );
    items[index] = updated;
    return updated;
  }

  @override
  Future<Item> updateServiceDuration(
    String itemId,
    UpdateServiceDurationRequest request,
  ) async {
    if (updateServiceDurationFailure != null) {
      throw updateServiceDurationFailure!;
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
      tingiMode: current.tingiMode,
      packagedSize: current.packagedSize,
      tingiIncrementStep: current.tingiIncrementStep,
      tingiAllowedSizes: current.tingiAllowedSizes,
      serviceDurationMinutes: request.durationMinutes,
      departmentId: current.departmentId,
      lowStockThreshold: current.lowStockThreshold,
    );
    items[index] = updated;
    return updated;
  }

  @override
  Future<List<ItemComboComponent>> listComboComponents(String itemId) async =>
      comboComponents;

  @override
  Future<ItemComboComponent> createComboComponent(
    String itemId,
    CreateItemComboComponentRequest request,
  ) async {
    if (createComboComponentFailure != null) {
      throw createComboComponentFailure!;
    }
    final category = categories.firstWhere(
      (category) => category.id == request.componentCategoryId,
    );
    final created = ItemComboComponent(
      id: 'combo-component-${comboComponents.length + 1}',
      componentCategoryId: category.id,
      componentCategoryName: category.name,
      slotLabel: request.slotLabel,
      quantity: request.quantity,
      substitutionUpchargeAmount: request.substitutionUpchargeAmount,
    );
    comboComponents.add(created);
    return created;
  }

  @override
  Future<Item> updateItemDepartment(
    String itemId,
    UpdateItemDepartmentRequest request,
  ) async {
    if (updateItemDepartmentFailure != null) {
      throw updateItemDepartmentFailure!;
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
      tingiMode: current.tingiMode,
      packagedSize: current.packagedSize,
      tingiIncrementStep: current.tingiIncrementStep,
      tingiAllowedSizes: current.tingiAllowedSizes,
      serviceDurationMinutes: current.serviceDurationMinutes,
      departmentId: request.departmentId,
      lowStockThreshold: current.lowStockThreshold,
    );
    items[index] = updated;
    return updated;
  }

  @override
  Future<Item> updateLowStockThreshold(
    String itemId,
    UpdateLowStockThresholdRequest request,
  ) async {
    lastUpdateLowStockThresholdRequest = request;
    if (updateLowStockThresholdFailure != null) {
      throw updateLowStockThresholdFailure!;
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
      tingiMode: current.tingiMode,
      packagedSize: current.packagedSize,
      tingiIncrementStep: current.tingiIncrementStep,
      tingiAllowedSizes: current.tingiAllowedSizes,
      serviceDurationMinutes: current.serviceDurationMinutes,
      departmentId: current.departmentId,
      lowStockThreshold: request.threshold,
    );
    items[index] = updated;
    return updated;
  }
}
