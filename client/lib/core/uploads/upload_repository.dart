import 'package:dio/dio.dart';

import '../network/api_client.dart';
import '../network/failure_mapper.dart';

/// Result of a successful `POST /uploads/image` call. `relativePath` (e.g.
/// `/uploads/{tenant}/{file}.jpg`) is what gets stored on the record (item,
/// category, tenant branding) — PurchImage resolves it against the current
/// server at display time, so it keeps working if the server address
/// changes (unlike storing the absolute `url`).
class UploadResult {
  const UploadResult({
    required this.relativePath,
    required this.url,
    required this.fileName,
  });

  factory UploadResult.fromJson(Map<String, dynamic> json) {
    return UploadResult(
      relativePath: json['relativePath'] as String,
      url: json['url'] as String,
      fileName: json['fileName'] as String,
    );
  }

  final String relativePath;
  final String url;
  final String fileName;
}

/// Talks to the backend's generic `POST /uploads/image` endpoint (10MB
/// limit, .jpg/.jpeg/.png/.webp/.gif/.svg only — enforced server-side) used
/// for branding logo, category image, and item image uploads alike.
class UploadRepository {
  UploadRepository({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  Future<UploadResult> uploadImage(String filePath) async {
    try {
      final fileName = filePath.split(RegExp(r'[\\/]')).last;
      final formData = FormData.fromMap({
        'file': await MultipartFile.fromFile(filePath, filename: fileName),
      });

      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/uploads/image',
        data: formData,
      );

      return UploadResult.fromJson(response.data!);
    } on DioException catch (error) {
      throw mapDioExceptionToFailure(error);
    }
  }
}
