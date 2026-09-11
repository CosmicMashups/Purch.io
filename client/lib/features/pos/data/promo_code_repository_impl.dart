import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/promo_code_models.dart';
import '../domain/promo_code_repository.dart';

class PromoCodeRepositoryImpl implements PromoCodeRepository {
  PromoCodeRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<PromoCode>> listPromoCodes() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>('/promo-codes');
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(PromoCode.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<PromoCode> createPromoCode(CreatePromoCodeRequest request) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/promo-codes',
        data: request.toJson(),
      );
      return PromoCode.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
