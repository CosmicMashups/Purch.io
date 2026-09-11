import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/bundle_promo_rule_models.dart';
import '../domain/catalog_repository.dart';
import '../domain/category_models.dart';
import '../domain/item_batch_models.dart';
import '../domain/item_models.dart';
import '../domain/item_variant_models.dart';
import '../domain/modifier_models.dart';

class CatalogRepositoryImpl implements CatalogRepository {
  CatalogRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<Category>> listCategories() {
    return _getList('/categories', Category.fromJson);
  }

  @override
  Future<Category> createCategory(CreateCategoryRequest request) {
    return _post('/categories', request.toJson(), Category.fromJson);
  }

  @override
  Future<List<Item>> listItems() {
    return _getList('/items', Item.fromJson);
  }

  @override
  Future<Item> createItem(CreateItemRequest request) {
    return _post('/items', request.toJson(), Item.fromJson);
  }

  @override
  Future<List<ModifierGroup>> listModifierGroups() {
    return _getList('/modifier-groups', ModifierGroup.fromJson);
  }

  @override
  Future<ModifierGroup> createModifierGroup(
    CreateModifierGroupRequest request,
  ) {
    return _post('/modifier-groups', request.toJson(), ModifierGroup.fromJson);
  }

  @override
  Future<ModifierGroup> addModifier(
    String groupId,
    CreateItemModifierRequest request,
  ) {
    return _post(
      '/modifier-groups/$groupId/modifiers',
      request.toJson(),
      ModifierGroup.fromJson,
    );
  }

  @override
  Future<List<ItemBatch>> listBatches(String itemId) {
    return _getList('/items/$itemId/batches', ItemBatch.fromJson);
  }

  @override
  Future<ItemBatch> receiveBatch(
    String itemId,
    CreateItemBatchRequest request,
  ) {
    return _post(
      '/items/$itemId/batches',
      request.toJson(),
      ItemBatch.fromJson,
    );
  }

  @override
  Future<List<BundlePromoRule>> listBundleRules(String itemId) {
    return _getList('/items/$itemId/bundle-rules', BundlePromoRule.fromJson);
  }

  @override
  Future<BundlePromoRule> createBundleRule(
    String itemId,
    CreateBundlePromoRuleRequest request,
  ) {
    return _post(
      '/items/$itemId/bundle-rules',
      request.toJson(),
      BundlePromoRule.fromJson,
    );
  }

  @override
  Future<List<ItemVariant>> listVariants(String itemId) {
    return _getList('/items/$itemId/variants', ItemVariant.fromJson);
  }

  @override
  Future<ItemVariant> createVariant(
    String itemId,
    CreateItemVariantRequest request,
  ) {
    return _post(
      '/items/$itemId/variants',
      request.toJson(),
      ItemVariant.fromJson,
    );
  }

  @override
  Future<List<ModifierGroup>> listModifierGroupsForItem(String itemId) {
    return _getList('/items/$itemId/modifier-groups', ModifierGroup.fromJson);
  }

  @override
  Future<ModifierGroup> attachModifierGroup(
    String itemId,
    AttachModifierGroupRequest request,
  ) {
    return _post(
      '/items/$itemId/modifier-groups',
      request.toJson(),
      ModifierGroup.fromJson,
    );
  }

  @override
  Future<Item> updateTingiConfig(
    String itemId,
    UpdateTingiConfigRequest request,
  ) {
    return _put('/items/$itemId/tingi-config', request.toJson(), Item.fromJson);
  }

  Future<T> _post<T>(
    String path,
    Map<String, dynamic> data,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        path,
        data: data,
      );
      return fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  Future<T> _put<T>(
    String path,
    Map<String, dynamic> data,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.put<Map<String, dynamic>>(
        path,
        data: data,
      );
      return fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  Future<List<T>> _getList<T>(
    String path,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(path);
      return response.data!.cast<Map<String, dynamic>>().map(fromJson).toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
