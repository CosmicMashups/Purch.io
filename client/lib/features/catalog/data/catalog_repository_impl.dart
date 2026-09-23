import 'dart:convert';

import 'package:dio/dio.dart';

import '../../../core/db/daos/catalog_cache_dao.dart';
import '../../../core/errors/failure.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/bundle_promo_rule_models.dart';
import '../domain/catalog_repository.dart';
import '../domain/category_models.dart';
import '../domain/item_batch_models.dart';
import '../domain/item_combo_component_models.dart';
import '../domain/item_models.dart';
import '../domain/item_variant_models.dart';
import '../domain/modifier_models.dart';

class CatalogRepositoryImpl implements CatalogRepository {
  /// [cache] and [tenantId] together switch on the offline-capable item and category lists: revalidated
  /// with the server's ETag, and served from the last good copy when the server can't be reached.
  /// [onFreshness] is told `null` after a confirmed-current load, or the time of the last confirmation
  /// when a stale copy had to be served.
  CatalogRepositoryImpl({
    required ApiClient apiClient,
    CatalogCacheDao? cache,
    Future<String?> Function()? tenantId,
    void Function(DateTime? staleSince)? onFreshness,
  }) : _apiClient = apiClient,
       _cache = cache,
       _tenantId = tenantId,
       _onFreshness = onFreshness;

  final ApiClient _apiClient;
  final CatalogCacheDao? _cache;
  final Future<String?> Function()? _tenantId;
  final void Function(DateTime? staleSince)? _onFreshness;

  @override
  Future<List<Category>> listCategories() {
    return _getCachedList('categories', '/categories', Category.fromJson);
  }

  @override
  Future<Category> createCategory(CreateCategoryRequest request) {
    return _post('/categories', request.toJson(), Category.fromJson);
  }

  @override
  Future<Category> updateCategory(
    String categoryId,
    UpdateCategoryRequest request,
  ) {
    return _put(
      '/categories/$categoryId',
      request.toJson(),
      Category.fromJson,
    );
  }

  @override
  Future<List<Item>> listItems() {
    return _getCachedList('items', '/items', Item.fromJson);
  }

  @override
  Future<Item> createItem(CreateItemRequest request) {
    return _post('/items', request.toJson(), Item.fromJson);
  }

  @override
  Future<Item> updateItem(String itemId, UpdateItemRequest request) {
    return _put('/items/$itemId', request.toJson(), Item.fromJson);
  }

  @override
  Future<List<ModifierGroup>> listModifierGroups() {
    return _getCachedList('modifier-groups', '/modifier-groups', ModifierGroup.fromJson);
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

  @override
  Future<Item> updateServiceDuration(
    String itemId,
    UpdateServiceDurationRequest request,
  ) {
    return _put(
      '/items/$itemId/service-duration',
      request.toJson(),
      Item.fromJson,
    );
  }

  @override
  Future<List<ItemComboComponent>> listComboComponents(String itemId) {
    return _getList(
      '/items/$itemId/combo-components',
      ItemComboComponent.fromJson,
    );
  }

  @override
  Future<ItemComboComponent> createComboComponent(
    String itemId,
    CreateItemComboComponentRequest request,
  ) {
    return _post(
      '/items/$itemId/combo-components',
      request.toJson(),
      ItemComboComponent.fromJson,
    );
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

  @override
  Future<Item> updateItemDepartment(
    String itemId,
    UpdateItemDepartmentRequest request,
  ) {
    return _put('/items/$itemId/department', request.toJson(), Item.fromJson);
  }

  @override
  Future<Item> updateLowStockThreshold(
    String itemId,
    UpdateLowStockThresholdRequest request,
  ) {
    return _put(
      '/items/$itemId/low-stock-threshold',
      request.toJson(),
      Item.fromJson,
    );
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

  Future<List<T>> _getCachedList<T>(
    String kind,
    String path,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    final cache = _cache;
    final tenant = cache == null ? null : await _tenantId?.call();
    if (cache == null || tenant == null) {
      return _getList(path, fromJson);
    }

    final cached = await cache.read(tenant, kind);
    // A copy the current app version can no longer parse is as good as none.
    final cachedItems = cached == null ? null : _decode(cached.payloadJson, fromJson);

    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        path,
        options: Options(
          headers: {
            if (cachedItems != null && cached?.etag != null)
              'If-None-Match': cached!.etag,
          },
          validateStatus: (status) =>
              status != null && ((status >= 200 && status < 300) || status == 304),
        ),
      );
      final now = DateTime.now();

      if (response.statusCode == 304 && cachedItems != null) {
        await cache.markValidated(tenant, kind, now);
        _onFreshness?.call(null);
        return cachedItems;
      }

      final body = response.data!;
      final items = body.cast<Map<String, dynamic>>().map(fromJson).toList();
      await cache.save(
        tenantId: tenant,
        kind: kind,
        etag: response.headers.value('etag'),
        payloadJson: jsonEncode(body),
        validatedAt: now,
      );
      _onFreshness?.call(null);
      return items;
    } on DioException catch (exception) {
      final failure = mapDioExceptionToFailure(exception);
      final unreachable =
          failure is NetworkFailure || failure is ServiceUnavailableFailure;
      if (unreachable && cachedItems != null) {
        _onFreshness?.call(cached!.validatedAt);
        return cachedItems;
      }
      throw failure;
    }
  }

  static List<T>? _decode<T>(
    String payloadJson,
    T Function(Map<String, dynamic>) fromJson,
  ) {
    try {
      return (jsonDecode(payloadJson) as List<dynamic>)
          .cast<Map<String, dynamic>>()
          .map(fromJson)
          .toList();
    } on Object {
      return null;
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
