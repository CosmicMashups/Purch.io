import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/shift_models.dart';
import '../domain/shift_repository.dart';

class ShiftRepositoryImpl implements ShiftRepository {
  ShiftRepositoryImpl({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<Shift?> getCurrentShift() async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(
        '/shifts/current',
      );
      final data = response.data;
      return data == null ? null : Shift.fromJson(data);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<Shift> openShift(OpenShiftRequest request) {
    return _post('/shifts/open', request.toJson());
  }

  @override
  Future<Shift> closeShift(CloseShiftRequest request) {
    return _post('/shifts/close', request.toJson());
  }

  Future<Shift> _post(String path, Map<String, dynamic> data) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        path,
        data: data,
      );
      return Shift.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
