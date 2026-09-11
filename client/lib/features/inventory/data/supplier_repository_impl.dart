import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/supplier_models.dart';
import '../domain/supplier_repository.dart';

class SupplierRepositoryImpl implements SupplierRepository {
  SupplierRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<Supplier>> listSuppliers() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>('/suppliers');
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(Supplier.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<Supplier> createSupplier(CreateSupplierRequest request) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/suppliers',
        data: request.toJson(),
      );
      return Supplier.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
