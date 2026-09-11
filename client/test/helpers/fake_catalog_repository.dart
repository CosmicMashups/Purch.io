import 'package:purch_client/features/catalog/domain/catalog_repository.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_batch_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/modifier_models.dart';

class FakeCatalogRepository implements CatalogRepository {
  FakeCatalogRepository({
    this.createCategoryFailure,
    this.createItemFailure,
    this.createModifierGroupFailure,
    this.addModifierFailure,
    this.receiveBatchFailure,
    List<Category>? initialCategories,
    List<Item>? initialItems,
    List<ModifierGroup>? initialModifierGroups,
    List<ItemBatch>? initialBatches,
  }) : categories = initialCategories ?? [],
       items = initialItems ?? [],
       modifierGroups = initialModifierGroups ?? [],
       batches = initialBatches ?? [];

  final Object? createCategoryFailure;
  final Object? createItemFailure;
  final Object? createModifierGroupFailure;
  final Object? addModifierFailure;
  final Object? receiveBatchFailure;
  final List<Category> categories;
  final List<Item> items;
  final List<ModifierGroup> modifierGroups;
  final List<ItemBatch> batches;

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
}
