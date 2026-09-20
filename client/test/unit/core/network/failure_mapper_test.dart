import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/core/network/failure_mapper.dart';

DioException _badResponse(int statusCode, Map<String, dynamic>? body) {
  final requestOptions = RequestOptions(path: '/auth/login');
  return DioException(
    requestOptions: requestOptions,
    type: DioExceptionType.badResponse,
    response: Response(
      requestOptions: requestOptions,
      statusCode: statusCode,
      data: body,
    ),
  );
}

void main() {
  group('mapDioExceptionToFailure', () {
    test(
      '400 with an errors object becomes a ValidationFailure with field errors',
      () {
        final failure = mapDioExceptionToFailure(
          _badResponse(400, {
            'title': 'Validation failed.',
            'errors': {
              'Pin': ['PIN is required.'],
            },
          }),
        );

        expect(failure, isA<ValidationFailure>());
        final validation = failure as ValidationFailure;
        expect(validation.fieldErrors['Pin'], ['PIN is required.']);
      },
    );

    test(
      '401 becomes an UnauthorizedFailure carrying the backend detail message',
      () {
        final failure = mapDioExceptionToFailure(
          _badResponse(401, {
            'detail': 'The device pairing code or PIN was not recognized.',
          }),
        );

        expect(failure, isA<UnauthorizedFailure>());
        expect(
          failure.message,
          'The device pairing code or PIN was not recognized.',
        );
      },
    );

    test('a 400 with no detail reads as the first specific field message, not the generic title', () {
      final failure = mapDioExceptionToFailure(
        _badResponse(400, {
          'title': 'Validation failed.',
          'errors': {
            'Quantity': ['Only 5 of Canned Goods on hand; can\'t ship 20.'],
          },
        }),
      );

      expect(failure, isA<ValidationFailure>());
      expect(failure.message, 'Only 5 of Canned Goods on hand; can\'t ship 20.');
      expect((failure as ValidationFailure).fieldErrors['Quantity'], isNotEmpty);
    });

    test('a 400 that has a detail keeps it', () {
      final failure = mapDioExceptionToFailure(
        _badResponse(400, {'detail': 'Explicit detail.', 'errors': {'X': ['other']}}),
      );
      expect(failure.message, 'Explicit detail.');
    });

    test('429 (rate limited) becomes a friendly retry-later failure', () {
      final failure = mapDioExceptionToFailure(
        _badResponse(429, {'title': 'Too Many Requests'}),
      );

      expect(failure, isA<ServiceUnavailableFailure>());
      expect(failure.message, contains('Too many attempts'));
    });

    test('403 becomes a ForbiddenFailure', () {
      final failure = mapDioExceptionToFailure(
        _badResponse(403, {'detail': 'Not allowed.'}),
      );
      expect(failure, isA<ForbiddenFailure>());
    });

    test('404 becomes a NotFoundFailure', () {
      final failure = mapDioExceptionToFailure(
        _badResponse(404, {'detail': 'Not found.'}),
      );
      expect(failure, isA<NotFoundFailure>());
    });

    test('409 becomes a ConflictFailure', () {
      final failure = mapDioExceptionToFailure(
        _badResponse(409, {'detail': 'Duplicate.'}),
      );
      expect(failure, isA<ConflictFailure>());
    });

    test('503 becomes a ServiceUnavailableFailure', () {
      final failure = mapDioExceptionToFailure(
        _badResponse(503, {'detail': 'DB unreachable.'}),
      );
      expect(failure, isA<ServiceUnavailableFailure>());
    });

    test(
      'an unrecognized status code becomes an UnknownFailure, not a crash',
      () {
        final failure = mapDioExceptionToFailure(
          _badResponse(418, {'detail': 'Teapot.'}),
        );
        expect(failure, isA<UnknownFailure>());
      },
    );

    test(
      'a body missing detail/title still produces a usable generic message',
      () {
        final failure = mapDioExceptionToFailure(_badResponse(500, null));
        expect(failure, isA<UnknownFailure>());
        expect(failure.message, isNotEmpty);
      },
    );

    test('connection errors (server unreachable) become a NetworkFailure', () {
      final requestOptions = RequestOptions(path: '/auth/login');
      final failure = mapDioExceptionToFailure(
        DioException(
          requestOptions: requestOptions,
          type: DioExceptionType.connectionError,
        ),
      );

      expect(failure, isA<NetworkFailure>());
    });
  });
}
