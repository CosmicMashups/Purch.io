import 'category_models.dart';
import 'item_models.dart';

/// B1/B2's base item form + B5's category half. Pricing-type sub-resources
/// (variant matrix, combo builder, bundle rules, weight/volume batches) and
/// modifier groups land alongside their own screens later.
abstract class CatalogRepository {
  Future<List<Category>> listCategories();

  Future<Category> createCategory(CreateCategoryRequest request);

  Future<List<Item>> listItems();

  Future<Item> createItem(CreateItemRequest request);
}
