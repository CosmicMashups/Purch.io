import 'dart:async';
import '../../../features/onboarding/domain/hardware_enums.dart';
import '../printer/printer_service.dart';

class DrawerOpenEvent {
  const DrawerOpenEvent({
    required this.timestamp,
    required this.reason,
    required this.operatorName,
    required this.wasManagerOverride,
  });

  final DateTime timestamp;
  final String reason;
  final String operatorName;
  final bool wasManagerOverride;
}

/// Service managing cash drawer RJ11 kick operations.
/// A physical POS cash drawer is an electronic solenoid connected via RJ11
/// directly into the thermal printer's kick port.
class CashDrawerService {
  CashDrawerService({
    required PrinterService printerService,
    CashDrawerPolicy defaultPolicy = CashDrawerPolicy.allowManualOpenWithManagerOverride,
    bool enabled = true,
  })  : _printerService = printerService,
        _policy = defaultPolicy,
        _enabled = enabled;

  final PrinterService _printerService;
  CashDrawerPolicy _policy;
  bool _enabled;

  final List<DrawerOpenEvent> _history = [];
  List<DrawerOpenEvent> get history => List.unmodifiable(_history);

  CashDrawerPolicy get policy => _policy;
  bool get isEnabled => _enabled;

  void updateSettings({required bool enabled, required CashDrawerPolicy policy}) {
    _enabled = enabled;
    _policy = policy;
  }

  /// Automatically kicks drawer open upon completing a cash tender transaction.
  Future<bool> kickOnCashSale({String operatorName = 'Cashier'}) async {
    if (!_enabled) return false;
    final success = await _printerService.kickDrawer();
    if (success) {
      _recordEvent(
        reason: 'Cash Tender Completion',
        operatorName: operatorName,
        wasManagerOverride: false,
      );
    }
    return success;
  }

  /// Manually opens the cash drawer ("No Sale / Open Drawer").
  /// Returns whether the drawer was kicked.
  /// Throws [CashDrawerPolicyException] if manual opening is disallowed by policy without manager override.
  Future<bool> openManual({
    required String operatorName,
    String reason = 'No Sale / Open Drawer',
    bool isManagerOverride = false,
  }) async {
    if (!_enabled) {
      throw CashDrawerException('Cash drawer is disabled in hardware settings.');
    }

    if (_policy == CashDrawerPolicy.kickOnSaleOnly && !isManagerOverride) {
      throw CashDrawerPolicyException(
        'Store policy requires cash sales only to open the cash drawer. Manager override required.',
      );
    }

    final success = await _printerService.kickDrawer();
    if (success) {
      _recordEvent(
        reason: reason,
        operatorName: operatorName,
        wasManagerOverride: isManagerOverride,
      );
    }
    return success;
  }

  void _recordEvent({
    required String reason,
    required String operatorName,
    required bool wasManagerOverride,
  }) {
    _history.insert(
      0,
      DrawerOpenEvent(
        timestamp: DateTime.now(),
        reason: reason,
        operatorName: operatorName,
        wasManagerOverride: wasManagerOverride,
      ),
    );
    if (_history.length > 100) {
      _history.removeLast();
    }
  }
}

class CashDrawerException implements Exception {
  CashDrawerException(this.message);
  final String message;
  @override
  String toString() => message;
}

class CashDrawerPolicyException implements Exception {
  CashDrawerPolicyException(this.message);
  final String message;
  @override
  String toString() => message;
}
