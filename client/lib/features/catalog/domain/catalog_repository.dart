import 'bundle_promo_rule_models.dart';
import 'category_models.dart';
import 'item_batch_models.dart';
import 'item_combo_component_models.dart';
import 'item_models.dart';
import 'item_variant_models.dart';
import 'modifier_models.dart';

/// B1/B2's base item form + B5 (categories, modifier groups, item-scoped
/// modifier group attachment for restaurant-style customization e.g. "No
/// Ice"/"No Pickles") + B2a (weight/volume batches, incl. tingi/sub-unit
/// selling config) + B2b (bundle rules) + B2c (service duration) + B3
/// (variant matrix) + B4 (combo/meal builder slots) + B6 (department/
/// concessionaire assignment — departments themselves live in the onboarding
/// feature since they belong to a branch, not the catalog).
abstract class CatalogRepository {
  Future<List<Category>> listCategories();

  Future<Category> createCategory(CreateCategoryRequest request);

  Future<Category> updateCategory(
    String categoryId,
    UpdateCategoryRequest request,
  );

  Future<List<Item>> listItems();

  Future<Item> createItem(CreateItemRequest request);

  Future<Item> updateItem(String itemId, UpdateItemRequest request);

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

  Future<List<ModifierGroup>> listModifierGroupsForItem(String itemId);

  Future<ModifierGroup> attachModifierGroup(
    String itemId,
    AttachModifierGroupRequest request,
  );

  Future<Item> updateTingiConfig(
    String itemId,
    UpdateTingiConfigRequest request,
  );

  Future<Item> updateServiceDuration(
    String itemId,
    UpdateServiceDurationRequest request,
  );

  Future<List<ItemComboComponent>> listComboComponents(String itemId);

  Future<ItemComboComponent> createComboComponent(
    String itemId,
    CreateItemComboComponentRequest request,
  );

  Future<Item> updateItemDepartment(
    String itemId,
    UpdateItemDepartmentRequest request,
  );

  Future<Item> updateLowStockThreshold(
    String itemId,
    UpdateLowStockThresholdRequest request,
  );
}
