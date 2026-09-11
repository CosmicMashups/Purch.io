import 'package:dio/dio.dart';

import '../errors/failure.dart';

/// Turns a caught DioException into the app's own Failure type by reading the
/// backend's ProblemDetails-shaped JSON body (title/detail/errors) — see
/// backend/src/Purch.Api/ErrorHandling/GlobalExceptionHandler.cs, which is
/// the thing on the other end producing this exact shape. Kept as a top-level
/// function (not a class) since it's pure and stateless — every repository
/// calls this the same way.
Failure mapDioExceptionToFailure(DioException exception) {
  switch (exception.type) {
    case DioExceptionType.connectionTimeout:
    case DioExceptionType.sendTimeout:
    case DioExceptionType.receiveTimeout:
    case DioExceptionType.transformTimeout:
    case DioExceptionType.connectionError:
      return const NetworkFailure(
        'Could not reach the server. Check your connection and try again.',
      );
    case DioExceptionType.cancel:
      return const NetworkFailure('The request was cancelled.');
    case DioExceptionType.badCertificate:
      return const NetworkFailure('Could not verify the server\'s identity.');
    case DioExceptionType.badResponse:
      return _mapBadResponse(exception);
    case DioExceptionType.unknown:
      return const NetworkFailure(
        'Could not reach the server. Check your connection and try again.',
      );
  }
}

Failure _mapBadResponse(DioException exception) {
  final statusCode = exception.response?.statusCode;
  final body = exception.response?.data;
  final problem =
      body is Map ? body.cast<String, dynamic>() : const <String, dynamic>{};

  final detail = problem['detail'] as String?;
  final title = problem['title'] as String?;
  final message = detail ?? title ?? 'Something went wrong. Please try again.';

  switch (statusCode) {
    case 400:
      final rawErrors = problem['errors'];
      final fieldErrors = <String, List<String>>{};
      if (rawErrors is Map) {
        for (final entry in rawErrors.entries) {
          final messages = entry.value;
          if (messages is List) {
            fieldErrors[entry.key as String] = messages.cast<String>();
          }
        }
      }
      return ValidationFailure(message, fieldErrors);
    case 401:
      return UnauthorizedFailure(message);
    case 403:
      return ForbiddenFailure(message);
    case 404:
      return NotFoundFailure(message);
    case 409:
      return ConflictFailure(message);
    case 503:
      return ServiceUnavailableFailure(message);
    default:
      return UnknownFailure(message);
  }
}
