import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/bir_reading_models.dart';
import '../domain/bir_reading_repository.dart';

class BirReadingRepositoryImpl implements BirReadingRepository {
  BirReadingRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<BirReading> generateXReading() => _post('/reports/x-reading');

  @override
  Future<BirReading> generateZReading() => _post('/reports/z-reading');

  Future<BirReading> _post(String path) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(path);
      return BirReading.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
