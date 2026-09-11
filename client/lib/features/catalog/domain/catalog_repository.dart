import 'category_models.dart';
import 'item_batch_models.dart';
import 'item_models.dart';
import 'modifier_models.dart';

/// B1/B2's base item form + B5 (categories, modifier groups) + B2a
/// (weight/volume batches). Remaining pricing-type sub-resources (variant
/// matrix, combo builder, bundle rules) land alongside their own screens.
abstract class CatalogRepository {
  Future<List<Category>> listCategories();

  Future<Category> createCategory(CreateCategoryRequest request);

  Future<List<Item>> listItems();

  Future<Item> createItem(CreateItemRequest request);

  Future<List<ModifierGroup>> listModifierGroups();

  Future<ModifierGroup> createModifierGroup(CreateModifierGroupRequest request);

  Future<ModifierGroup> addModifier(
    String groupId,
    CreateItemModifierRequest request,
  );

  Future<List<ItemBatch>> listBatches(String itemId);

  Future<ItemBatch> receiveBatch(String itemId, CreateItemBatchRequest request);
}
