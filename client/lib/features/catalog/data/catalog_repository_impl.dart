import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/catalog_repository.dart';
import '../domain/category_models.dart';
import '../domain/item_models.dart';

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
