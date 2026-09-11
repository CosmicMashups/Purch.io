import 'bundle_promo_rule_models.dart';
import 'category_models.dart';
import 'item_batch_models.dart';
import 'item_models.dart';
import 'item_variant_models.dart';
import 'modifier_models.dart';

/// B1/B2's base item form + B5 (categories, modifier groups) + B2a
/// (weight/volume batches) + B2b (bundle rules) + B3 (variant matrix).
/// Remaining pricing-type sub-resources: B2c (service duration), B4 (combo).
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

  Future<List<BundlePromoRule>> listBundleRules(String itemId);

  Future<BundlePromoRule> createBundleRule(
    String itemId,
    CreateBundlePromoRuleRequest request,
  );

  Future<List<ItemVariant>> listVariants(String itemId);

  Future<ItemVariant> createVariant(
    String itemId,
    CreateItemVariantRequest request,
  );
}
