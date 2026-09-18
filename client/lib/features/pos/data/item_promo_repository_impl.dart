import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/item_promo_models.dart';
import '../domain/item_promo_repository.dart';

class ItemPromoRepositoryImpl implements ItemPromoRepository {
  ItemPromoRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<BogoPromoRule>> listBogoPromoRules() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/promos/bogo',
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(BogoPromoRule.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<BogoPromoRule> createBogoPromoRule(
    CreateBogoPromoRuleRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/promos/bogo',
        data: request.toJson(),
      );
      return BogoPromoRule.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<BogoPromoRule> updateBogoPromoRule(
    String id,
    UpdateBogoPromoRuleRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.put<Map<String, dynamic>>(
        '/promos/bogo/$id',
        data: request.toJson(),
      );
      return BogoPromoRule.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<List<ComboPromoRule>> listComboPromoRules() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/promos/combos',
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(ComboPromoRule.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<ComboPromoRule> createComboPromoRule(
    CreateComboPromoRuleRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/promos/combos',
        data: request.toJson(),
      );
      return ComboPromoRule.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<ComboPromoRule> updateComboPromoRule(
    String id,
    UpdateComboPromoRuleRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.put<Map<String, dynamic>>(
        '/promos/combos/$id',
        data: request.toJson(),
      );
      return ComboPromoRule.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<List<ItemDiscountPromoRule>> listItemDiscountPromoRules() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/promos/item-discounts',
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(ItemDiscountPromoRule.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<ItemDiscountPromoRule> createItemDiscountPromoRule(
    CreateItemDiscountPromoRuleRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/promos/item-discounts',
        data: request.toJson(),
      );
      return ItemDiscountPromoRule.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<ItemDiscountPromoRule> updateItemDiscountPromoRule(
    String id,
    UpdateItemDiscountPromoRuleRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.put<Map<String, dynamic>>(
        '/promos/item-discounts/$id',
        data: request.toJson(),
      );
      return ItemDiscountPromoRule.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
