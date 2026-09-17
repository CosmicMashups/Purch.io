import 'dart:async';
import 'esc_pos_builder.dart';
import 'esc_pos_commands.dart';
import 'printer_transport.dart';
import 'receipt_esc_pos_formatter.dart';
import '../../../features/onboarding/domain/tenant_settings_models.dart';
import '../../../features/pos/domain/transaction_models.dart';

enum PrinterStatus { ready, printing, offline, error }

class PrinterService {
  PrinterService({PrinterTransport? transport, PaperWidth defaultPaperWidth = PaperWidth.mm80})
      : _transport = transport ?? MockPrinterTransport(),
        _paperWidth = defaultPaperWidth;

  PrinterTransport _transport;
  PaperWidth _paperWidth;
  PrinterStatus _status = PrinterStatus.ready;

  PrinterTransport get transport => _transport;
  PaperWidth get paperWidth => _paperWidth;
  PrinterStatus get status => _status;

  void updateTransport(PrinterTransport newTransport) {
    _transport.disconnect();
    _transport = newTransport;
  }

  void updatePaperWidth(PaperWidth width) {
    _paperWidth = width;
  }

  /// Sends raw ESC/POS bytes directly to printer.
  Future<bool> sendRaw(List<int> bytes) async {
    _status = PrinterStatus.printing;
    try {
      await _transport.send(bytes);
      _status = PrinterStatus.ready;
      return true;
    } catch (e) {
      _status = PrinterStatus.error;
      return false;
    }
  }

  /// Prints a formatted BIR-compliant receipt for a transaction.
  Future<bool> printReceipt({
    required Transaction transaction,
    TenantSettings? tenantSettings,
    String? branchName,
    String? branchAddress,
    String? cashierName,
    bool cutPaper = true,
    bool kickDrawer = false,
    bool pin5Drawer = false,
  }) async {
    final bytes = ReceiptEscPosFormatter.format(
      transaction: transaction,
      tenantSettings: tenantSettings,
      branchName: branchName,
      branchAddress: branchAddress,
      cashierName: cashierName,
      paperWidth: _paperWidth,
      cutPaper: cutPaper,
      kickDrawer: kickDrawer,
      pin5Drawer: pin5Drawer,
    );

    return sendRaw(bytes);
  }

  /// Sends the cash drawer kick pulse command via printer.
  Future<bool> kickDrawer({bool pin5 = false}) async {
    final bytes = pin5
        ? EscPosCommands.drawerKickPin5()
        : EscPosCommands.drawerKickPin2();
    return sendRaw(bytes);
  }

  /// Prints a diagnostic test print slip.
  Future<bool> printTestPage({String title = 'Purch.io Hardware Diagnostic'}) async {
    final builder = EscPosBuilder(paperWidth: _paperWidth);
    builder.align(PrintAlignment.center);
    builder.text('*** TEST PRINT ***', isBold: true, widthMultiplier: 2, heightMultiplier: 2);
    builder.text(title);
    builder.text('Printer: ${_transport.type.name.toUpperCase()}');
    builder.text('Paper Width: ${_paperWidth == PaperWidth.mm80 ? "80mm (48 col)" : "58mm (32 col)"}');
    builder.divider('-');
    builder.align(PrintAlignment.left);
    builder.twoColumn('Alignment Left', 'Right Align');
    builder.text('Regular Text: 0123456789 ABCDEFGHIJKLMNOP');
    builder.text('Bold Emphasized Text', isBold: true);
    builder.text('Underlined Text', isBold: false);
    builder.divider('-');
    builder.align(PrintAlignment.center);
    builder.qrCode('https://purch.io/hardware-test', moduleSize: 4);
    builder.feedLines(1);
    builder.text('ESC/POS Diagnostic Passed OK!');
    builder.cut(feedLines: 3, partial: true);

    return sendRaw(builder.toBytes());
  }

  Future<bool> testConnection() async {
    return _transport.testConnection();
  }

  void dispose() {
    _transport.disconnect();
  }
}
