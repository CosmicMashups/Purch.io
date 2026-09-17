import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/hardware/drawer/cash_drawer_service.dart';
import 'package:purch_client/core/hardware/printer/printer_service.dart';
import 'package:purch_client/core/hardware/printer/printer_transport.dart';
import 'package:purch_client/features/onboarding/domain/hardware_enums.dart';

void main() {
  group('CashDrawerService', () {
    late MockPrinterTransport mockTransport;
    late PrinterService printerService;
    late CashDrawerService drawerService;

    setUp(() {
      mockTransport = MockPrinterTransport();
      printerService = PrinterService(transport: mockTransport);
      drawerService = CashDrawerService(
        printerService: printerService,
        defaultPolicy: CashDrawerPolicy.allowManualOpenWithManagerOverride,
        enabled: true,
      );
    });

    test('kickOnCashSale triggers 24V pulse command and records event', () async {
      final success = await drawerService.kickOnCashSale(operatorName: 'Juan');

      expect(success, isTrue);
      expect(mockTransport.drawerKickCount, equals(1));
      expect(drawerService.history.length, equals(1));
      expect(drawerService.history.first.reason, equals('Cash Tender Completion'));
      expect(drawerService.history.first.operatorName, equals('Juan'));
    });

    test('openManual succeeds when policy is allowManualOpenWithManagerOverride', () async {
      final success = await drawerService.openManual(
        operatorName: 'Maria',
        reason: 'Change breakdown',
      );

      expect(success, isTrue);
      expect(mockTransport.drawerKickCount, equals(1));
      expect(drawerService.history.first.reason, equals('Change breakdown'));
    });

    test('openManual throws CashDrawerPolicyException when kickOnSaleOnly without override', () async {
      drawerService.updateSettings(
        enabled: true,
        policy: CashDrawerPolicy.kickOnSaleOnly,
      );

      expect(
        () => drawerService.openManual(operatorName: 'Cashier'),
        throwsA(isA<CashDrawerPolicyException>()),
      );
      expect(mockTransport.drawerKickCount, equals(0));
    });

    test('openManual succeeds under kickOnSaleOnly if isManagerOverride is true', () async {
      drawerService.updateSettings(
        enabled: true,
        policy: CashDrawerPolicy.kickOnSaleOnly,
      );

      final success = await drawerService.openManual(
        operatorName: 'Manager Jose',
        reason: 'Supervisor override',
        isManagerOverride: true,
      );

      expect(success, isTrue);
      expect(mockTransport.drawerKickCount, equals(1));
      expect(drawerService.history.first.wasManagerOverride, isTrue);
    });
  });
}
