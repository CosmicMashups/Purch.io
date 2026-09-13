import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/kiosk_branding_repository.dart';

class KioskBrandingRepositoryImpl implements KioskBrandingRepository {
  KioskBrandingRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<KioskBranding> getBranding() async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(
        '/kiosk/branding',
      );
      return KioskBranding.fromJson(response.data ?? {});
    } on DioException catch (e) {
      throw mapDioExceptionToFailure(e);
    }
  }
}
