import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/core/network/api_client.dart';
import 'package:purch_client/core/storage/secure_token_storage.dart';
import 'package:purch_client/features/catalog/data/catalog_repository_impl.dart';

String _jwt() {
  String part(Map<String, Object?> json) =>
      base64Url.encode(utf8.encode(jsonEncode(json))).replaceAll('=', '');
  final exp = DateTime.now().add(const Duration(hours: 2)).millisecondsSinceEpoch ~/ 1000;
  return '${part({'alg': 'none'})}.${part({'exp': exp})}.sig';
}

class _Tokens extends SecureTokenStorage {
  @override
  Future<String?> readAccessToken() async => _jwt();

  @override
  Future<String?> readRefreshToken() async => 'refresh';

  @override
  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) async {}

  @override
  Future<void> clear() async {}
}

/// Plays back a scripted server: each request takes the next response, and the request headers are recorded.
class _ScriptedServer implements HttpClientAdapter {
  final script = <FutureOr<ResponseBody> Function(RequestOptions)>[];
  final requests = <RequestOptions>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    return script.removeAt(0)(options);
  }

  @override
  void close({bool force = false}) {}
}

ResponseBody _categories(List<Map<String, Object?>> rows, {String? etag}) =>
    ResponseBody.fromString(
      jsonEncode(rows),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
        if (etag != null) 'etag': [etag],
      },
    );

ResponseBody _notModified() => ResponseBody.fromString('', 304);

ResponseBody _unreachable(RequestOptions options) => throw DioException(
  requestOptions: options,
  type: DioExceptionType.connectionError,
);

Map<String, Object?> _category(String id, String name) => {
  'id': id,
  'name': name,
  'sortOrder': 0,
  'imageUrl': null,
};

void main() {
  late AppDatabase database;
  late _ScriptedServer server;
  late List<DateTime?> freshness;
  late String? tenant;
  late CatalogRepositoryImpl repository;

  setUp(() {
    database = AppDatabase.forTesting(NativeDatabase.memory());
    server = _ScriptedServer();
    freshness = [];
    tenant = 'tenant-a';
    final dio = Dio(BaseOptions(baseUrl: 'http://test'))..httpClientAdapter = server;
    repository = CatalogRepositoryImpl(
      apiClient: ApiClient(tokenStorage: _Tokens(), dio: dio, refreshDio: dio),
      cache: database.catalogCacheDao,
      tenantId: () async => tenant,
      onFreshness: freshness.add,
    );
  });

  tearDown(() => database.close());

  test('a 304 is answered from the saved copy and sends the saved ETag', () async {
    server.script
      ..add((_) => _categories([_category('c1', 'Drinks')], etag: 'W/"v1"'))
      ..add((_) => _notModified());

    final first = await repository.listCategories();
    final second = await repository.listCategories();

    expect(first.single.name, 'Drinks');
    expect(second.single.name, 'Drinks');
    expect(server.requests[0].headers.containsKey('If-None-Match'), isFalse);
    expect(server.requests[1].headers['If-None-Match'], 'W/"v1"');
    expect(freshness, [null, null]); // both loads were confirmed current
  });

  test('a changed catalog replaces the saved copy and its ETag', () async {
    server.script
      ..add((_) => _categories([_category('c1', 'Drinks')], etag: 'W/"v1"'))
      ..add((_) => _categories([_category('c1', 'Beverages')], etag: 'W/"v2"'))
      ..add((_) => _notModified());

    await repository.listCategories();
    final changed = await repository.listCategories();
    final again = await repository.listCategories();

    expect(changed.single.name, 'Beverages');
    expect(again.single.name, 'Beverages');
    expect(server.requests[2].headers['If-None-Match'], 'W/"v2"');
  });

  test('when the server is unreachable the saved copy is served and marked stale', () async {
    server.script
      ..add((_) => _categories([_category('c1', 'Drinks')], etag: 'W/"v1"'))
      ..add(_unreachable);

    await repository.listCategories();
    final offline = await repository.listCategories();

    expect(offline.single.name, 'Drinks');
    expect(freshness.last, isNotNull);
    final saved = await database.catalogCacheDao.read('tenant-a', 'categories');
    expect(freshness.last, saved!.validatedAt);
  });

  test('with nothing saved, an unreachable server is still an error', () async {
    server.script.add(_unreachable);

    await expectLater(repository.listCategories(), throwsA(isA<NetworkFailure>()));
    expect(freshness, isEmpty);
  });

  test('another business on the same device never sees the saved copy', () async {
    server.script
      ..add((_) => _categories([_category('c1', 'Drinks')], etag: 'W/"v1"'))
      ..add(_unreachable);

    await repository.listCategories();
    tenant = 'tenant-b';

    await expectLater(repository.listCategories(), throwsA(isA<NetworkFailure>()));
  });

  test('a saved copy the app can no longer parse is ignored, not trusted', () async {
    await database.catalogCacheDao.save(
      tenantId: 'tenant-a',
      kind: 'categories',
      etag: 'W/"old"',
      payloadJson: '[{"unexpected": true}]',
      validatedAt: DateTime.now(),
    );
    server.script.add((_) => _categories([_category('c1', 'Drinks')], etag: 'W/"v9"'));

    final loaded = await repository.listCategories();

    expect(loaded.single.name, 'Drinks');
    // The unusable copy's ETag was not offered, so the server could not 304 us into using it.
    expect(server.requests.single.headers.containsKey('If-None-Match'), isFalse);
  });
}
