import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/bootstrap_models.dart';
import '../domain/onboarding_repository.dart';
import '../domain/staff_models.dart';

class OnboardingRepositoryImpl implements OnboardingRepository {
  OnboardingRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<BootstrapResult> bootstrap(BootstrapRequest request) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/onboarding/bootstrap',
        data: request.toJson(),
      );
      return BootstrapResult.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<List<StaffMember>> listStaff() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>('/staff');
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(StaffMember.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<StaffMember> createStaff(CreateStaffRequest request) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/staff',
        data: request.toJson(),
      );
      return StaffMember.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<StaffMember> updateStaff(
    String staffId,
    UpdateStaffRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.put<Map<String, dynamic>>(
        '/staff/$staffId',
        data: request.toJson(),
      );
      return StaffMember.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
