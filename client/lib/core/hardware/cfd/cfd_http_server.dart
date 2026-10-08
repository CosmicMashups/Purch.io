import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:qr/qr.dart';

import 'cfd_models.dart';

/// Embedded HTTP and WebSocket server for tethered tablets, secondary browser
/// displays, and remote customer devices.
class CfdHttpServer {
  CfdHttpServer({this.port = 8088});

  final int port;
  HttpServer? _server;
  final Set<WebSocket> _clients = {};
  CfdState _currentState = const CfdState();

  bool get isRunning => _server != null;
  int get connectedClientsCount => _clients.length;

  Future<bool> start() async {
    if (_server != null) return true;
    try {
      _server = await HttpServer.bind(InternetAddress.anyIPv4, port);
      _server!.listen(_handleRequest);
      return true;
    } catch (_) {
      _server = null;
      return false;
    }
  }

  Future<void> stop() async {
    for (final client in _clients) {
      await client.close();
    }
    _clients.clear();
    await _server?.close(force: true);
    _server = null;
  }

  void broadcastState(CfdState state) {
    _currentState = state;
    final jsonString = jsonEncode(_stateJson(state));

    final deadClients = <WebSocket>[];
    for (final client in _clients) {
      try {
        client.add(jsonString);
      } catch (_) {
        deadClients.add(client);
      }
    }
    _clients.removeAll(deadClients);
  }

  /// The state plus, while a QR Ph payment is showing, the QR as rows of '1'/'0' modules. The page draws it on a
  /// canvas, so the payload never leaves the till for an online QR service and it works offline.
  static Map<String, dynamic> _stateJson(CfdState state) {
    final json = state.toJson();
    final payload = state.qrPhPayload;
    if (payload != null && payload.isNotEmpty) {
      final image = QrImage(QrCode.fromData(data: payload, errorCorrectLevel: QrErrorCorrectLevel.M));
      json['qrMatrix'] = [
        for (var y = 0; y < image.moduleCount; y++)
          [for (var x = 0; x < image.moduleCount; x++) image.isDark(y, x) ? '1' : '0'].join(),
      ];
    }
    return json;
  }

  /// True when there is no Origin header (a non-browser client) or its host:port equals the Host the
  /// request was sent to.
  static bool isSameOriginOrAbsent(String? origin, String? host) {
    if (origin == null || origin.isEmpty) return true;
    final uri = Uri.tryParse(origin);
    if (uri == null || host == null) return false;
    final originAuthority = uri.hasPort ? '${uri.host}:${uri.port}' : uri.host;
    return originAuthority == host;
  }

  void _handleRequest(HttpRequest request) async {
    final path = request.uri.path;

    if (path == '/cfd/ws') {
      // Browsers do not apply CORS to WebSockets, so without this any web page open on the LAN could
      // connect and read the live cart. The display page itself connects same-origin; non-browser
      // clients send no Origin and are unaffected.
      if (!isSameOriginOrAbsent(request.headers.value('origin'), request.headers.host)) {
        request.response.statusCode = HttpStatus.forbidden;
        await request.response.close();
        return;
      }
      if (WebSocketTransformer.isUpgradeRequest(request)) {
        final socket = await WebSocketTransformer.upgrade(request);
        _clients.add(socket);
        socket.add(jsonEncode(_stateJson(_currentState)));
        socket.listen(
          (_) {},
          onDone: () => _clients.remove(socket),
          onError: (_) => _clients.remove(socket),
        );
      } else {
        request.response.statusCode = HttpStatus.badRequest;
        await request.response.close();
      }
      return;
    }

    if (path == '/cfd/state') {
      request.response.headers.contentType = ContentType.json;
      request.response.write(jsonEncode(_stateJson(_currentState)));
      await request.response.close();
      return;
    }

    // Serve HTML5 / CSS / JS Customer Facing Display
    if (path == '/cfd' || path == '/' || path == '/cfd/') {
      request.response.headers.contentType = ContentType.html;
      request.response.headers.set('Content-Security-Policy', _contentSecurityPolicy);
      request.response.headers.set('X-Content-Type-Options', 'nosniff');
      request.response.headers.set('X-Frame-Options', 'DENY');
      request.response.headers.set('Referrer-Policy', 'no-referrer');
      request.response.write(_generateHtmlUi());
      await request.response.close();
      return;
    }

    request.response.statusCode = HttpStatus.notFound;
    await request.response.close();
  }

  /// The page is self-contained apart from Google Fonts and the QR image service, so it may talk to
  /// nothing else: injected markup can neither load foreign scripts nor send what it reads elsewhere,
  /// and the page cannot be framed by another site.
  static const _contentSecurityPolicy = "default-src 'none'; "
      "script-src 'unsafe-inline'; "
      "style-src 'unsafe-inline' https://fonts.googleapis.com; "
      'font-src https://fonts.gstatic.com; '
      "img-src 'self' data:; "
      "connect-src 'self'; "
      "frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

  String _generateHtmlUi() {
    return '''
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Customer Facing Display — Purch.io</title>
  <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@600;700;800&family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@500;700;800&display=swap" rel="stylesheet">
  <style>
    :root {
      --bg: #0B0F19;
      --card: #131B2E;
      --card-border: #1E293B;
      --text-main: #F8FAFC;
      --text-muted: #94A3B8;
      --emerald: #10B981;
      --emerald-glow: rgba(16, 185, 129, 0.15);
      --primary: #3B82F6;
      --amber: #F59E0B;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      background: var(--bg);
      color: var(--text-main);
      font-family: 'Plus Jakarta Sans', sans-serif;
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
    header {
      padding: 20px 36px;
      border-bottom: 1px solid var(--card-border);
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: var(--card);
    }
    .store-brand {
      font-family: 'Outfit', sans-serif;
      font-size: 26px;
      font-weight: 800;
      letter-spacing: -0.5px;
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .badge {
      background: var(--emerald-glow);
      color: var(--emerald);
      border: 1px solid var(--emerald);
      padding: 4px 12px;
      border-radius: 999px;
      font-size: 13px;
      font-weight: 600;
    }
    main {
      flex: 1;
      display: grid;
      grid-template-columns: 1.4fr 1fr;
      padding: 28px 36px;
      gap: 32px;
      overflow: hidden;
    }
    .panel {
      background: var(--card);
      border: 1px solid var(--card-border);
      border-radius: 20px;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
    .items-header {
      padding: 18px 24px;
      border-bottom: 1px solid var(--card-border);
      font-weight: 700;
      color: var(--text-muted);
      font-size: 14px;
      letter-spacing: 0.5px;
      text-transform: uppercase;
      display: grid;
      grid-template-columns: 1fr 80px 120px;
    }
    .items-list {
      flex: 1;
      overflow-y: auto;
      padding: 12px 24px;
    }
    .item-row {
      display: grid;
      grid-template-columns: 1fr 80px 120px;
      padding: 14px 0;
      border-bottom: 1px solid rgba(255,255,255,0.05);
      align-items: center;
      font-size: 17px;
    }
    .item-name { font-weight: 600; }
    .item-qty { color: var(--text-muted); text-align: center; }
    .item-total { font-family: 'JetBrains Mono', monospace; font-weight: 700; text-align: right; }

    .summary-panel {
      padding: 32px;
      justify-content: space-between;
    }
    .total-box {
      background: rgba(16, 185, 129, 0.08);
      border: 1px solid rgba(16, 185, 129, 0.3);
      border-radius: 16px;
      padding: 24px;
      text-align: right;
    }
    .total-label { font-size: 16px; color: var(--text-muted); font-weight: 600; }
    .total-value {
      font-family: 'JetBrains Mono', monospace;
      font-size: 48px;
      font-weight: 800;
      color: var(--emerald);
      margin-top: 4px;
    }

    .qr-container {
      margin-top: 24px;
      background: white;
      padding: 20px;
      border-radius: 16px;
      text-align: center;
      display: none;
    }
    .qr-container img {
      max-width: 220px;
      height: auto;
    }
    .qr-prompt {
      color: #0F172A;
      font-weight: 700;
      font-size: 15px;
      margin-top: 10px;
    }
    .idle-view {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100%;
      text-align: center;
      gap: 16px;
    }
    .idle-title { font-family: 'Outfit', sans-serif; font-size: 36px; font-weight: 800; }
    .idle-desc { font-size: 18px; color: var(--text-muted); max-width: 440px; }
  </style>
</head>
<body>
  <header>
    <div class="store-brand" id="store-title">Purch.io Store</div>
    <div class="badge" id="status-badge">● LIVE REGISTER</div>
  </header>

  <main>
    <section class="panel">
      <div class="items-header">
        <span>Item Description</span>
        <span style="text-align: center;">Qty</span>
        <span style="text-align: right;">Amount</span>
      </div>
      <div class="items-list" id="items-box">
        <div class="idle-view" id="idle-banner">
          <div class="idle-title">Maligayang Pagdating!</div>
          <div class="idle-desc">Your scanned items and savings will appear here live.</div>
        </div>
      </div>
    </section>

    <section class="panel summary-panel">
      <div>
        <div style="display: flex; justify-content: space-between; margin-bottom: 12px; font-size: 16px;">
          <span style="color: var(--text-muted);">Subtotal</span>
          <span style="font-family: 'JetBrains Mono', monospace;" id="subtotal">PHP 0.00</span>
        </div>
        <div style="display: flex; justify-content: space-between; margin-bottom: 12px; font-size: 16px;" id="discount-row">
          <span style="color: var(--text-muted);">Discounts</span>
          <span style="font-family: 'JetBrains Mono', monospace; color: var(--emerald);" id="discount">-PHP 0.00</span>
        </div>
        <div style="display: flex; justify-content: space-between; margin-bottom: 4px; font-size: 14px;">
          <span style="color: var(--text-muted);">VATable Sales</span>
          <span style="font-family: 'JetBrains Mono', monospace; color: var(--text-muted);" id="vatable-sales">PHP 0.00</span>
        </div>
        <div style="display: flex; justify-content: space-between; margin-bottom: 12px; font-size: 14px;">
          <span style="color: var(--text-muted);">VAT (12%)</span>
          <span style="font-family: 'JetBrains Mono', monospace; color: var(--text-muted);" id="vat-amount">PHP 0.00</span>
        </div>
        <div class="total-box">
          <div class="total-label">TOTAL TO PAY (VAT Inclusive)</div>
          <div class="total-value" id="grand-total">PHP 0.00</div>
        </div>
      </div>

      <div class="qr-container" id="qr-section">
        <canvas id="qr-canvas" width="220" height="220" role="img" aria-label="Dynamic QR Ph"></canvas>
        <div class="qr-prompt">Scan with GCash, Maya, or any QR Ph App</div>
      </div>

      <div id="change-box" style="display: none; background: rgba(59, 130, 246, 0.1); border: 1px solid var(--primary); padding: 16px; border-radius: 12px; text-align: right;">
        <span style="font-size: 14px; color: var(--text-muted);">CHANGE DUE</span>
        <div style="font-family: 'JetBrains Mono', monospace; font-size: 28px; font-weight: 700; color: var(--primary);" id="change-val">PHP 0.00</div>
      </div>
    </section>
  </main>

  <script>
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `\${wsProtocol}//\${window.location.host}/cfd/ws`;
    let socket;

    function connect() {
      socket = new WebSocket(wsUrl);
      socket.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);
          updateUi(data);
        } catch (err) {
          console.error(err);
        }
      };
      socket.onclose = () => setTimeout(connect, 2000);
    }

    function drawQr(canvas, rows) {
      const quiet = 2;
      const modules = rows.length + quiet * 2;
      const cell = Math.floor(canvas.width / modules);
      const offset = Math.floor((canvas.width - cell * modules) / 2);
      const ctx = canvas.getContext('2d');
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.fillStyle = '#000000';
      rows.forEach((row, y) => {
        for (let x = 0; x < row.length; x++) {
          if (row[x] === '1') ctx.fillRect(offset + (x + quiet) * cell, offset + (y + quiet) * cell, cell, cell);
        }
      });
    }

    function updateUi(state) {
      document.getElementById('store-title').innerText = state.storeName || 'Purch.io Store';
      document.getElementById('subtotal').innerText = `PHP \${state.subtotal.toFixed(2)}`;
      document.getElementById('discount').innerText = `-PHP \${state.discountAmount.toFixed(2)}`;
      document.getElementById('grand-total').innerText = `PHP \${state.totalAmount.toFixed(2)}`;
      document.getElementById('vatable-sales').innerText = `PHP \${(state.vatableSales || 0).toFixed(2)}`;
      document.getElementById('vat-amount').innerText = `PHP \${(state.vatAmount || 0).toFixed(2)}`;

      // Built with textContent, never an HTML string: item names and the welcome message are staff-editable
      // catalog/tenant text and must not be able to run script in this page.
      const itemsBox = document.getElementById('items-box');
      const el = (cls, text) => {
        const node = document.createElement('div');
        node.className = cls;
        node.textContent = text;
        return node;
      };
      itemsBox.replaceChildren();
      if (!state.lines || state.lines.length === 0) {
        const idle = el('idle-view', '');
        idle.append(
          el('idle-title', state.welcomeMessage || 'Maligayang Pagdating!'),
          el('idle-desc', 'Your items will appear here as the cashier scans them.'),
        );
        itemsBox.append(idle);
      } else {
        for (const line of state.lines) {
          const row = el('item-row', '');
          row.append(
            el('item-name', String(line.name)),
            el('item-qty', String(line.quantity)),
            el('item-total', '₱' + Number(line.lineTotal).toFixed(2)),
          );
          itemsBox.append(row);
        }
        itemsBox.scrollTop = itemsBox.scrollHeight;
      }

      const qrSection = document.getElementById('qr-section');
      if (state.qrMatrix && state.qrMatrix.length > 0) {
        qrSection.style.display = 'block';
        drawQr(document.getElementById('qr-canvas'), state.qrMatrix);
      } else {
        qrSection.style.display = 'none';
      }

      const changeBox = document.getElementById('change-box');
      if (state.changeGiven !== undefined && state.changeGiven !== null && state.changeGiven > 0) {
        changeBox.style.display = 'block';
        document.getElementById('change-val').innerText = `PHP \${state.changeGiven.toFixed(2)}`;
      } else {
        changeBox.style.display = 'none';
      }
    }

    connect();
  </script>
</body>
</html>
''';
  }
}
