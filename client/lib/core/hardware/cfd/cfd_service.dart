import 'dart:async';
import 'dart:convert';
import 'cfd_http_server.dart';
import 'cfd_models.dart';
import '../printer/printer_transport.dart';
import '../../../features/pos/domain/payment_method.dart';
import '../../../features/pos/domain/transaction_models.dart';

class CfdService {
  CfdService({
    CfdHttpServer? httpServer,
    PrinterTransport? vfdPoleTransport,
    bool autoStartServer = false,
  })  : _httpServer = httpServer ?? CfdHttpServer(),
        _vfdPoleTransport = vfdPoleTransport {
    if (autoStartServer) {
      _httpServer.start();
    }
  }

  final CfdHttpServer _httpServer;
  final PrinterTransport? _vfdPoleTransport;
  CfdState _currentState = const CfdState();

  final _stateController = StreamController<CfdState>.broadcast();

  CfdState get currentState => _currentState;
  Stream<CfdState> get stateStream => _stateController.stream;
  bool get isServerRunning => _httpServer.isRunning;
  int get connectedClientsCount => _httpServer.connectedClientsCount;
  int get serverPort => _httpServer.port;

  Future<bool> startServer() async {
    final started = await _httpServer.start();
    if (started) {
      _httpServer.broadcastState(_currentState);
    }
    return started;
  }

  Future<void> stopServer() => _httpServer.stop();

  /// Updates CFD state from the current active cart transaction.
  void updateFromTransaction({
    required Transaction transaction,
    String? storeName,
    String? storeLogoUrl,
    PaymentMethod? activePaymentMethod,
    String? dynamicQrPayload,
  }) {
    final lines = transaction.lines.map((l) {
      return CfdLineItem(
        name: l.itemName,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        lineTotal: l.lineTotal,
      );
    }).toList();

    CfdMode mode = CfdMode.cart;
    if (transaction.status == TransactionStatus.completed) {
      mode = CfdMode.completed;
    } else if (dynamicQrPayload != null && dynamicQrPayload.isNotEmpty) {
      mode = CfdMode.payment;
    } else if (lines.isEmpty) {
      mode = CfdMode.idle;
    }

    double? change;
    double? tendered;
    if (transaction.payments.isNotEmpty) {
      for (final p in transaction.payments) {
        if (p.changeGiven != null) change = p.changeGiven;
        if (p.amountTendered != null) tendered = p.amountTendered;
      }
    }

    // Mirrors the BIR VAT breakdown in receipt_esc_pos_formatter.dart so the
    // customer display, printed receipt, and X/Z readings never disagree.
    final isSeniorPwd = transaction.seniorPwdDiscountApplied;
    final vatableSales =
        isSeniorPwd ? 0.0 : (transaction.totalAmount / 1.12);
    final vatAmount =
        isSeniorPwd ? 0.0 : (transaction.totalAmount - vatableSales);

    _currentState = _currentState.copyWith(
      mode: mode,
      storeName: storeName ?? _currentState.storeName,
      storeLogoUrl: storeLogoUrl ?? _currentState.storeLogoUrl,
      lines: lines,
      subtotal: transaction.subtotal,
      // DiscountAmount already includes a promo code's discount; item promos are
      // tracked separately. Adding promoDiscountAmount again double-counted the
      // code, so the display's subtotal - discount never matched the total.
      discountAmount:
          transaction.discountAmount + transaction.itemPromoDiscountAmount,
      totalAmount: transaction.totalAmount,
      vatableSales: vatableSales,
      vatAmount: vatAmount,
      paymentMethod: activePaymentMethod,
      qrPhPayload: dynamicQrPayload,
      amountTendered: tendered,
      changeGiven: change,
      receiptNumber: transaction.receiptNumber,
    );

    _stateController.add(_currentState);
    _httpServer.broadcastState(_currentState);

    // Update physical 20x2 VFD pole display if attached
    _updateVfdPoleDisplay(transaction);
  }

  /// Formats and pushes text to a 20x2 character VFD pole display.
  Future<void> _updateVfdPoleDisplay(Transaction transaction) async {
    if (_vfdPoleTransport == null) return;

    try {
      final bytes = <int>[];
      // Clear VFD display (0x0C or ESC @)
      bytes.addAll([0x1B, 0x40, 0x0C]);

      if (transaction.lines.isEmpty) {
        // Line 1: Welcome message
        bytes.addAll(utf8.encode('Welcome to Store!   \r\n'));
        // Line 2: Ready
        bytes.addAll(utf8.encode('Next Customer Please\r\n'));
      } else {
        // Line 1: Last scanned item name & price (max 20 chars)
        final lastLine = transaction.lines.last;
        final name = lastLine.itemName.length > 11
            ? lastLine.itemName.substring(0, 11)
            : lastLine.itemName.padRight(11);
        final price = 'P${lastLine.lineTotal.toStringAsFixed(2)}'.padLeft(8);
        bytes.addAll(utf8.encode('$name $price\r\n'));

        // Line 2: Total amount (max 20 chars)
        final totalStr = 'TOTAL P${transaction.totalAmount.toStringAsFixed(2)}'.padLeft(20);
        bytes.addAll(utf8.encode('$totalStr\r\n'));
      }

      await _vfdPoleTransport.send(bytes);
    } catch (_) {
      // Non-blocking fallback for pole displays
    }
  }

  void resetToIdle() {
    _currentState = _currentState.copyWith(
      mode: CfdMode.idle,
      lines: [],
      subtotal: 0.0,
      discountAmount: 0.0,
      totalAmount: 0.0,
      vatableSales: 0.0,
      vatAmount: 0.0,
      qrPhPayload: null,
      amountTendered: null,
      changeGiven: null,
      receiptNumber: null,
    );
    _stateController.add(_currentState);
    _httpServer.broadcastState(_currentState);
  }

  void dispose() {
    _stateController.close();
    _httpServer.stop();
  }
}
