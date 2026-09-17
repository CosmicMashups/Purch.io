import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/hardware/cfd/cfd_models.dart';
import 'package:purch_client/core/hardware/cfd/cfd_service.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

void main() {
  group('CfdService', () {
    late CfdService cfdService;

    setUp(() {
      cfdService = CfdService(autoStartServer: false);
    });

    test('initial state is idle', () {
      expect(cfdService.currentState.mode, equals(CfdMode.idle));
      expect(cfdService.currentState.lines, isEmpty);
      expect(cfdService.currentState.totalAmount, equals(0.0));
    });

    test('updates lines and total when items are rung up', () {
      final tx = const Transaction(
        id: 'tx-1',
        branchId: 'b-1',
        deviceId: 'd-1',
        status: TransactionStatus.open,
        lines: [
          TransactionLine(
            id: 'l-1',
            itemId: 'i-1',
            itemName: 'San Miguel Pale Pilsen',
            itemVariantId: null,
            quantity: 3,
            unitPrice: 65.0,
            lineTotal: 195.0,
            comboSelections: [],
          ),
        ],
        subtotal: 195.0,
        discountAmount: 0.0,
        seniorPwdDiscountApplied: false,
        promoCode: null,
        promoDiscountAmount: 0.0,
        totalAmount: 195.0,
        receiptNumber: null,
        payments: [],
      );

      cfdService.updateFromTransaction(transaction: tx, storeName: 'Corner Store');

      expect(cfdService.currentState.mode, equals(CfdMode.cart));
      expect(cfdService.currentState.storeName, equals('Corner Store'));
      expect(cfdService.currentState.lines.length, equals(1));
      expect(cfdService.currentState.lines.first.name, equals('San Miguel Pale Pilsen'));
      expect(cfdService.currentState.totalAmount, equals(195.0));
    });

    test('switches to payment mode when dynamic QR Ph is passed', () {
      final tx = const Transaction(
        id: 'tx-1',
        branchId: 'b-1',
        deviceId: 'd-1',
        status: TransactionStatus.awaitingPayment,
        lines: [
          TransactionLine(
            id: 'l-1',
            itemId: 'i-1',
            itemName: 'Item 1',
            itemVariantId: null,
            quantity: 1,
            unitPrice: 100.0,
            lineTotal: 100.0,
            comboSelections: [],
          ),
        ],
        subtotal: 100.0,
        discountAmount: 0.0,
        seniorPwdDiscountApplied: false,
        promoCode: null,
        promoDiscountAmount: 0.0,
        totalAmount: 100.0,
        receiptNumber: null,
        payments: [],
      );

      cfdService.updateFromTransaction(
        transaction: tx,
        activePaymentMethod: PaymentMethod.qrPh,
        dynamicQrPayload: '00020101021226...5405100.005802PH...',
      );

      expect(cfdService.currentState.mode, equals(CfdMode.payment));
      expect(cfdService.currentState.qrPhPayload, contains('5405100.00'));
      expect(cfdService.currentState.paymentMethod, equals(PaymentMethod.qrPh));
    });
  });
}
