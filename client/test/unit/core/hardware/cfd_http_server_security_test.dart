import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/hardware/cfd/cfd_http_server.dart';
import 'package:purch_client/core/hardware/cfd/cfd_models.dart';

void main() {
  group('CfdHttpServer security', () {
    late CfdHttpServer server;
    late HttpClient http;
    const port = 18088;

    setUp(() async {
      server = CfdHttpServer(port: port);
      expect(await server.start(), isTrue);
      http = HttpClient();
    });

    tearDown(() async {
      http.close(force: true);
      await server.stop();
    });

    Future<HttpClientResponse> get(String path) async {
      final request = await http.get('127.0.0.1', port, path);
      return request.close();
    }

    test('display page never uses innerHTML, so item names cannot inject script', () async {
      final response = await get('/cfd');
      final body = await response.transform(utf8.decoder).join();
      expect(RegExp(r'\.(innerHTML|outerHTML)\s*=|insertAdjacentHTML').hasMatch(body), isFalse);
      expect(body, contains('textContent'));
    });

    test('pages carry a CSP that forbids framing and foreign connections', () async {
      final response = await get('/cfd');
      await response.drain<void>();
      final csp = response.headers.value('content-security-policy') ?? '';
      expect(csp, contains("frame-ancestors 'none'"));
      expect(csp, contains("connect-src 'self'"));
      expect(response.headers.value('x-content-type-options'), 'nosniff');
    });

    test('no wildcard CORS on the page or the state endpoint', () async {
      for (final path in ['/cfd', '/cfd/state']) {
        final response = await get(path);
        await response.drain<void>();
        expect(response.headers.value('access-control-allow-origin'), isNull, reason: path);
      }
    });

    test('websocket from a foreign web origin is refused', () async {
      final request = await http.get('127.0.0.1', port, '/cfd/ws');
      request.headers.set('Origin', 'https://evil.example');
      final response = await request.close();
      await response.drain<void>();
      expect(response.statusCode, HttpStatus.forbidden);
    });

    test('the QR Ph payload is drawn locally and never sent to a third-party QR service', () async {
      final page = await (await get('/cfd')).transform(utf8.decoder).join();
      expect(page, isNot(contains('qrserver')));

      server.broadcastState(const CfdState(qrPhPayload: '00020101021226580014ph.ppmi.p2m'));
      final state = jsonDecode(await (await get('/cfd/state')).transform(utf8.decoder).join()) as Map<String, dynamic>;
      final rows = (state['qrMatrix'] as List).cast<String>();
      expect(rows, isNotEmpty);
      expect(rows.every((row) => row.length == rows.length && RegExp(r'^[01]+$').hasMatch(row)), isTrue);

      server.broadcastState(const CfdState());
      final idle = jsonDecode(await (await get('/cfd/state')).transform(utf8.decoder).join()) as Map<String, dynamic>;
      expect(idle.containsKey('qrMatrix'), isFalse);
    });

    test('same-origin check', () {
      expect(CfdHttpServer.isSameOriginOrAbsent(null, '192.168.1.5:8088'), isTrue);
      expect(CfdHttpServer.isSameOriginOrAbsent('http://192.168.1.5:8088', '192.168.1.5:8088'), isTrue);
      expect(CfdHttpServer.isSameOriginOrAbsent('http://evil.example', '192.168.1.5:8088'), isFalse);
      expect(CfdHttpServer.isSameOriginOrAbsent('http://192.168.1.5:9999', '192.168.1.5:8088'), isFalse);
    });
  });
}
