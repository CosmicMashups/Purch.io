import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/hardware/printer/receipt_esc_pos_formatter.dart';
import 'package:purch_client/features/onboarding/domain/onboarding_enums.dart';
import 'package:purch_client/features/onboarding/domain/tenant_settings_models.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

void main() {
  group('ReceiptEscPosFormatter', () {
    final sampleTx = Transaction(
      id: 'tx-test-12345678',
      branchId: 'branch-1',
      deviceId: 'pos-term-01',
      status: TransactionStatus.completed,
      receiptNumber: 1042,
      subtotal: 280.0,
      discountAmount: 0.0,
      seniorPwdDiscountApplied: false,
      promoCode: null,
      promoDiscountAmount: 0.0,
      totalAmount: 280.0,
      lines: const [
        TransactionLine(
          id: 'line-1',
          itemId: 'item-1',
          itemName: 'Datu Puti Vinegar 1L',
          itemVariantId: null,
          quantity: 2,
          unitPrice: 40.0,
          lineTotal: 80.0,
          comboSelections: [],
        ),
        TransactionLine(
          id: 'line-2',
          itemId: 'item-2',
          itemName: 'Pork Belly (Liempo)',
          itemVariantId: null,
          quantity: 0.5,
          unitPrice: 400.0,
          lineTotal: 200.0,
          comboSelections: [],
        ),
      ],
      payments: const [
        Payment(
          id: 'pay-1',
          method: PaymentMethod.cash,
          status: PaymentStatus.confirmed,
          amount: 280.0,
          amountTendered: 500.0,
          changeGiven: 220.0,
        ),
      ],
    );

    final sampleSettings = const TenantSettings(
      id: 'tenant-1',
      name: 'Aling Nena Store',
      businessType: BusinessType.groceryStore,
      brandingLogoUrl: null,
      brandingBackgroundColorHex: null,
      brandingAccentColorHex: null,
      brandingPrimaryTextColorHex: null,
      brandingSecondaryTextColorHex: null,
      brandingFontFamily: null,
      requiresBarcodePerItem: false,
      tin: '123-456-789-000',
      registeredBusinessName: 'Aling Nena Enterprise Inc.',
      registeredAddress: '123 Rizal Ave, Manila',
      creditLedgerRetentionDays: 30,
      creditLedgerEnabled: true,
      kioskPosterImageUrl: null,
    );

    test('generates valid BIR receipt byte stream with header, items and tax breakdown', () {
      final bytes = ReceiptEscPosFormatter.format(
        transaction: sampleTx,
        tenantSettings: sampleSettings,
        branchName: 'Main Store',
        cashierName: 'Maria S.',
        cutPaper: true,
        kickDrawer: true,
      );

      final decoded = utf8.decode(bytes, allowMalformed: true);

      // Business & BIR Header
      expect(decoded, contains('Aling Nena Enterprise Inc.'));
      expect(decoded, contains('123-456-789-000'));
      expect(decoded, contains('123 Rizal Ave, Manila'));
      expect(decoded, contains('OR-00001042'));
      expect(decoded, contains('Maria S.'));

      // Line items
      expect(decoded, contains('Datu Puti Vinegar 1L'));
      expect(decoded, contains('Pork Belly (Liempo)'));
      expect(decoded, contains('0.500')); // Fractional scale weight formatted

      // Totals
      expect(decoded, contains('TOTAL AMOUNT'));
      expect(decoded, contains('PHP 280.00'));

      // VAT Math: 280 / 1.12 = 250.00 VATable, 30.00 VAT
      expect(decoded, contains('VATable Sales:'));
      expect(decoded, contains('PHP 250.00'));
      expect(decoded, contains('VAT Amount (12%):'));
      expect(decoded, contains('PHP 30.00'));

      // Payment & Change
      expect(decoded, contains('CASH TENDERED'));
      expect(decoded, contains('PHP 500.00'));
      expect(decoded, contains('CHANGE'));
      expect(decoded, contains('PHP 220.00'));

      // BIR Disclaimer
      expect(decoded, contains('THIS SERVES AS AN OFFICIAL RECEIPT'));
    });

    Transaction discounted({
      required double discountAmount,
      required double promoDiscountAmount,
      required bool senior,
      String? promoCode,
      double itemPromo = 0,
    }) => Transaction(
      id: sampleTx.id,
      branchId: sampleTx.branchId,
      deviceId: sampleTx.deviceId,
      status: TransactionStatus.completed,
      receiptNumber: sampleTx.receiptNumber,
      subtotal: 280.0,
      discountAmount: discountAmount,
      seniorPwdDiscountApplied: senior,
      promoCode: promoCode,
      promoDiscountAmount: promoDiscountAmount,
      itemPromoDiscountAmount: itemPromo,
      totalAmount: 280.0 - itemPromo - discountAmount,
      lines: sampleTx.lines,
      payments: sampleTx.payments,
    );

    String receiptFor(Transaction tx) => utf8.decode(
      ReceiptEscPosFormatter.format(
        transaction: tx,
        tenantSettings: sampleSettings,
        branchName: 'Main Store',
        cashierName: 'Maria S.',
        cutPaper: false,
        kickDrawer: false,
      ),
      allowMalformed: true,
    );

    test('a Senior/PWD sale prints only the Senior/PWD discount, even with a promo code on the cart', () {
      // The code is still on the cart but gave nothing (the two never combine).
      final text = receiptFor(
        discounted(discountAmount: 56, promoDiscountAmount: 0, senior: true, promoCode: 'SAVE10'),
      );

      expect(text, contains('Senior/PWD Discount (20%)'));
      expect(text, contains('-PHP 56.00'));
      expect(text, isNot(contains('Promo (SAVE10)')));
      expect(text, contains('PHP 224.00')); // 280 - 56
    });

    test('a promo-code sale prints one promo line, not a duplicate generic Discount line', () {
      final text = receiptFor(
        discounted(discountAmount: 28, promoDiscountAmount: 28, senior: false, promoCode: 'SAVE10'),
      );

      expect(text, contains('Promo (SAVE10)'));
      expect(text, contains('-PHP 28.00'));
      expect(text, isNot(contains('Senior/PWD')));
      expect('Discount'.allMatches(text).length, 0);
    });

    test('item promos are printed so subtotal minus discounts reconciles with the total', () {
      final text = receiptFor(
        discounted(discountAmount: 0, promoDiscountAmount: 0, senior: false, itemPromo: 40),
      );

      expect(text, contains('Item promos'));
      expect(text, contains('-PHP 40.00'));
      expect(text, contains('PHP 240.00')); // 280 - 40
    });
  });
}
