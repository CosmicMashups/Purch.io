import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/network/api_client.dart';
import 'package:purch_client/core/storage/secure_token_storage.dart';

String _jwt({required DateTime expires}) {
  String part(Map<String, Object?> json) =>
      base64Url.encode(utf8.encode(jsonEncode(json))).replaceAll('=', '');
  return '${part({'alg': 'none'})}.'
      '${part({'exp': expires.millisecondsSinceEpoch ~/ 1000})}.sig';
}

class _MemoryTokenStorage extends SecureTokenStorage {
  String? access;
  String? refresh;

  @override
  Future<String?> readAccessToken() async => access;

  @override
  Future<String?> readRefreshToken() async => refresh;

  @override
  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    access = accessToken;
    refresh = refreshToken;
  }

  @override
  Future<void> clear() async {
    access = null;
    refresh = null;
  }
}

typedef _Handler = FutureOr<ResponseBody> Function(RequestOptions options);

/// Routes requests to per-path handlers and records what was sent.
class _FakeAdapter implements HttpClientAdapter {
  final handlers = <String, _Handler>{};
  final requests = <RequestOptions>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    final handler = handlers[options.path];
    if (handler == null) {
      throw StateError('No handler for ${options.path}');
    }
    return handler(options);
  }

  @override
  void close({bool force = false}) {}
}

ResponseBody _json(int status, Map<String, Object?> body) => ResponseBody.fromString(
  jsonEncode(body),
  status,
  headers: {
    Headers.contentTypeHeader: [Headers.jsonContentType],
  },
);

void main() {
  late _MemoryTokenStorage storage;
  late _FakeAdapter api;
  late _FakeAdapter refreshApi;
  late int sessionExpiredCalls;
  late ApiClient client;

  final freshAccess = _jwt(expires: DateTime.now().add(const Duration(minutes: 30)));

  setUp(() {
    storage = _MemoryTokenStorage();
    api = _FakeAdapter();
    refreshApi = _FakeAdapter();
    sessionExpiredCalls = 0;

    final dio = Dio(BaseOptions(baseUrl: 'http://test'))..httpClientAdapter = api;
    final refreshDio =
        Dio(BaseOptions(baseUrl: 'http://test'))..httpClientAdapter = refreshApi;
    client = ApiClient(
      tokenStorage: storage,
      dio: dio,
      refreshDio: refreshDio,
      onSessionExpired: () => sessionExpiredCalls++,
    );
  });

  String? sentAuth(int index) => api.requests[index].headers['Authorization'] as String?;

  test('a token that is about to expire is renewed before the request is sent', () async {
    storage
      ..access = _jwt(expires: DateTime.now().add(const Duration(seconds: 30)))
      ..refresh = 'refresh-1';
    refreshApi.handlers['/auth/refresh'] = (_) => _json(200, {
      'accessToken': freshAccess,
      'refreshToken': 'refresh-2',
    });
    api.handlers['/items'] = (_) => _json(200, {'ok': true});

    await client.dio.get<dynamic>('/items');

    expect(refreshApi.requests, hasLength(1));
    expect(api.requests, hasLength(1), reason: 'no failed attempt + retry');
    expect(sentAuth(0), 'Bearer $freshAccess');
    expect(storage.refresh, 'refresh-2');
  });

  test('a token with plenty of life left is not refreshed', () async {
    storage
      ..access = freshAccess
      ..refresh = 'refresh-1';
    api.handlers['/items'] = (_) => _json(200, {'ok': true});

    await client.dio.get<dynamic>('/items');

    expect(refreshApi.requests, isEmpty);
    expect(sentAuth(0), 'Bearer $freshAccess');
  });

  test('a 401 triggers one refresh and the request is retried with the new token', () async {
    storage
      ..access = freshAccess
      ..refresh = 'refresh-1';
    final renewed = _jwt(expires: DateTime.now().add(const Duration(hours: 1)));
    refreshApi.handlers['/auth/refresh'] = (_) => _json(200, {
      'accessToken': renewed,
      'refreshToken': 'refresh-2',
    });
    var calls = 0;
    api.handlers['/items'] =
        (_) => ++calls == 1 ? _json(401, {'title': 'expired'}) : _json(200, {'ok': true});

    final response = await client.dio.get<dynamic>('/items');

    expect(response.statusCode, 200);
    expect(sentAuth(1), 'Bearer $renewed');
    expect(sessionExpiredCalls, 0);
  });

  test('concurrent expired requests share a single refresh (the token is single-use)', () async {
    storage
      ..access = _jwt(expires: DateTime.now().subtract(const Duration(minutes: 1)))
      ..refresh = 'refresh-1';
    refreshApi.handlers['/auth/refresh'] = (_) async {
      await Future<void>.delayed(const Duration(milliseconds: 20));
      return _json(200, {'accessToken': freshAccess, 'refreshToken': 'refresh-2'});
    };
    api.handlers['/a'] = (_) => _json(200, {});
    api.handlers['/b'] = (_) => _json(200, {});
    api.handlers['/c'] = (_) => _json(200, {});

    await Future.wait([
      client.dio.get<dynamic>('/a'),
      client.dio.get<dynamic>('/b'),
      client.dio.get<dynamic>('/c'),
    ]);

    expect(refreshApi.requests, hasLength(1));
  });

  test('a refresh that fails on the server (503) keeps the session', () async {
    storage
      ..access = freshAccess
      ..refresh = 'refresh-1';
    refreshApi.handlers['/auth/refresh'] = (_) => _json(503, {'title': 'down'});
    api.handlers['/items'] = (_) => _json(401, {'title': 'expired'});

    await expectLater(
      client.dio.get<dynamic>('/items'),
      throwsA(isA<DioException>()),
    );

    expect(storage.refresh, 'refresh-1', reason: 'tokens must survive a server hiccup');
    expect(storage.access, freshAccess);
    expect(sessionExpiredCalls, 0);
  });

  test('a refresh that never reaches the server (network error) keeps the session', () async {
    storage
      ..access = freshAccess
      ..refresh = 'refresh-1';
    refreshApi.handlers['/auth/refresh'] = (options) =>
        throw DioException.connectionError(
          requestOptions: options,
          reason: 'offline',
        );
    api.handlers['/items'] = (_) => _json(401, {'title': 'expired'});

    await expectLater(client.dio.get<dynamic>('/items'), throwsA(isA<DioException>()));

    expect(storage.refresh, 'refresh-1');
    expect(sessionExpiredCalls, 0);
  });

  test('a refresh token the server rejects (401) ends the session', () async {
    storage
      ..access = freshAccess
      ..refresh = 'refresh-1';
    refreshApi.handlers['/auth/refresh'] = (_) => _json(401, {'title': 'invalid'});
    api.handlers['/items'] = (_) => _json(401, {'title': 'expired'});

    await expectLater(client.dio.get<dynamic>('/items'), throwsA(isA<DioException>()));

    expect(storage.access, isNull);
    expect(storage.refresh, isNull);
    expect(sessionExpiredCalls, 1);
  });

  test('login and refresh endpoints are never sent through the refresh dance', () async {
    storage
      ..access = _jwt(expires: DateTime.now().subtract(const Duration(hours: 1)))
      ..refresh = 'refresh-1';
    api.handlers['/auth/login'] = (_) => _json(401, {'title': 'wrong pin'});

    await expectLater(
      client.dio.post<dynamic>('/auth/login', data: {}),
      throwsA(isA<DioException>()),
    );

    expect(refreshApi.requests, isEmpty);
    expect(sessionExpiredCalls, 0);
  });
}
