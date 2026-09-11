import 'package:purch_client/features/catalog/domain/catalog_repository.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';

class FakeCatalogRepository implements CatalogRepository {
  FakeCatalogRepository({
    this.createCategoryFailure,
    this.createItemFailure,
    List<Category>? initialCategories,
    List<Item>? initialItems,
  }) : categories = initialCategories ?? [],
       items = initialItems ?? [];

  final Object? createCategoryFailure;
  final Object? createItemFailure;
  final List<Category> categories;
  final List<Item> items;

  CreateItemRequest? lastCreateItemRequest;

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
}
