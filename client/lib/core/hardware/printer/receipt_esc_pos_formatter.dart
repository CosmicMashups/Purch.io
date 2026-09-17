import '../../../features/onboarding/domain/tenant_settings_models.dart';
import '../../../features/pos/domain/payment_method.dart';
import '../../../features/pos/domain/transaction_models.dart';
import 'esc_pos_builder.dart';

/// Converts a completed or in-progress [Transaction] into a BIR-compliant
/// ESC/POS thermal receipt byte stream for 58mm or 80mm commercial printers.
class ReceiptEscPosFormatter {
  const ReceiptEscPosFormatter._();

  static List<int> format({
    required Transaction transaction,
    TenantSettings? tenantSettings,
    String? branchName,
    String? branchAddress,
    String? cashierName,
    PaperWidth paperWidth = PaperWidth.mm80,
    bool cutPaper = true,
    bool kickDrawer = false,
    bool pin5Drawer = false,
    String? customFooter,
  }) {
    final builder = EscPosBuilder(paperWidth: paperWidth);

    // 1. Business Header (BIR compliance)
    builder.align(PrintAlignment.center);
    final businessName =
        tenantSettings?.registeredBusinessName ??
        tenantSettings?.name ??
        'Purch.io Store';
    builder.text(businessName, isBold: true, widthMultiplier: 2, heightMultiplier: 2);

    if (tenantSettings?.name != null &&
        tenantSettings?.registeredBusinessName != null &&
        tenantSettings!.name != tenantSettings.registeredBusinessName) {
      builder.text(tenantSettings.name);
    }

    final address =
        branchAddress ?? tenantSettings?.registeredAddress ?? 'Manila, Philippines';
    builder.text(address);

    if (tenantSettings?.tin != null && tenantSettings!.tin!.isNotEmpty) {
      builder.text('VAT Reg TIN: ${tenantSettings.tin}');
    }
    if (branchName != null && branchName.isNotEmpty) {
      builder.text('Branch: $branchName');
    }
    builder.text('Terminal ID: ${transaction.deviceId.substring(0, transaction.deviceId.length > 8 ? 8 : transaction.deviceId.length)}');

    builder.divider('=');

    // 2. Transaction Details
    builder.align(PrintAlignment.left);
    final receiptNo = transaction.receiptNumber != null
        ? 'OR-${transaction.receiptNumber!.toString().padLeft(8, '0')}'
        : 'TX-${transaction.id.substring(0, 8)}';
    builder.twoColumn('Invoice No:', receiptNo, isBold: true);

    final now = DateTime.now();
    final dateStr =
        '${now.year}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')} '
        '${now.hour.toString().padLeft(2, '0')}:${now.minute.toString().padLeft(2, '0')}';
    builder.twoColumn('Date & Time:', dateStr);

    if (cashierName != null && cashierName.isNotEmpty) {
      builder.twoColumn('Cashier:', cashierName);
    }
    if (transaction.orderType != null && transaction.orderType!.isNotEmpty) {
      builder.twoColumn('Order Type:', transaction.orderType!);
    }

    builder.divider('-');

    // 3. Line Items Table Header
    if (paperWidth == PaperWidth.mm80) {
      builder.tableRow([
        const TableColumn(text: 'ITEM', widthRatio: 0.44, alignment: PrintAlignment.left),
        const TableColumn(text: 'QTY', widthRatio: 0.16, alignment: PrintAlignment.right),
        const TableColumn(text: 'PRICE', widthRatio: 0.20, alignment: PrintAlignment.right),
        const TableColumn(text: 'TOTAL', widthRatio: 0.20, alignment: PrintAlignment.right),
      ]);
    } else {
      // 58mm narrower paper
      builder.tableRow([
        const TableColumn(text: 'ITEM', widthRatio: 0.42, alignment: PrintAlignment.left),
        const TableColumn(text: 'QTY', widthRatio: 0.20, alignment: PrintAlignment.right),
        const TableColumn(text: 'TOTAL', widthRatio: 0.38, alignment: PrintAlignment.right),
      ]);
    }
    builder.divider('-');

    // 4. Line Items Table Rows
    for (final line in transaction.lines) {
      final qtyFormatted = line.quantity.truncateToDouble() == line.quantity
          ? line.quantity.toInt().toString()
          : line.quantity.toStringAsFixed(3);

      if (paperWidth == PaperWidth.mm80) {
        builder.tableRow([
          TableColumn(text: line.itemName, widthRatio: 0.44, alignment: PrintAlignment.left),
          TableColumn(text: qtyFormatted, widthRatio: 0.16, alignment: PrintAlignment.right),
          TableColumn(
            text: line.unitPrice.toStringAsFixed(2),
            widthRatio: 0.20,
            alignment: PrintAlignment.right,
          ),
          TableColumn(
            text: '${line.lineTotal.toStringAsFixed(2)} V',
            widthRatio: 0.20,
            alignment: PrintAlignment.right,
          ),
        ]);
      } else {
        builder.tableRow([
          TableColumn(text: line.itemName, widthRatio: 0.42, alignment: PrintAlignment.left),
          TableColumn(text: 'x$qtyFormatted', widthRatio: 0.20, alignment: PrintAlignment.right),
          TableColumn(
            text: '${line.lineTotal.toStringAsFixed(2)} V',
            widthRatio: 0.38,
            alignment: PrintAlignment.right,
          ),
        ]);
      }

      // Combo components indent
      for (final combo in line.comboSelections) {
        builder.text('  + ${combo.slotLabel}: ${combo.selectedItemName}');
      }
    }

    builder.divider('-');

    // 5. Totals & Discounts
    builder.twoColumn('Subtotal', 'PHP ${transaction.subtotal.toStringAsFixed(2)}');

    if (transaction.seniorPwdDiscountApplied && transaction.discountAmount > 0) {
      builder.twoColumn(
        'Senior/PWD Discount (20%)',
        '-PHP ${transaction.discountAmount.toStringAsFixed(2)}',
        isBold: true,
      );
    } else if (transaction.discountAmount > 0) {
      builder.twoColumn(
        'Discount',
        '-PHP ${transaction.discountAmount.toStringAsFixed(2)}',
      );
    }

    if (transaction.promoCode != null && transaction.promoDiscountAmount > 0) {
      builder.twoColumn(
        'Promo (${transaction.promoCode})',
        '-PHP ${transaction.promoDiscountAmount.toStringAsFixed(2)}',
      );
    }

    builder.divider('=');
    builder.twoColumn(
      'TOTAL AMOUNT',
      'PHP ${transaction.totalAmount.toStringAsFixed(2)}',
      isBold: true,
    );
    builder.divider('=');

    // 6. BIR Philippine Tax Breakdown (12% VAT standard)
    final isSeniorPwd = transaction.seniorPwdDiscountApplied;
    final vatableSales = isSeniorPwd ? 0.0 : (transaction.totalAmount / 1.12);
    final vatAmount = isSeniorPwd ? 0.0 : (transaction.totalAmount - vatableSales);
    final vatExemptSales = isSeniorPwd ? transaction.totalAmount : 0.0;
    const zeroRatedSales = 0.0;

    builder.twoColumn('VATable Sales:', 'PHP ${vatableSales.toStringAsFixed(2)}');
    builder.twoColumn('VAT Amount (12%):', 'PHP ${vatAmount.toStringAsFixed(2)}');
    builder.twoColumn('VAT-Exempt Sales:', 'PHP ${vatExemptSales.toStringAsFixed(2)}');
    builder.twoColumn('Zero-Rated Sales:', 'PHP ${zeroRatedSales.toStringAsFixed(2)}');
    builder.divider('-');

    // 7. Payments Tendered & Change
    if (transaction.payments.isNotEmpty) {
      for (final payment in transaction.payments) {
        final methodName = switch (payment.method) {
          PaymentMethod.cash => 'CASH TENDERED',
          PaymentMethod.manualGcashQr => 'GCASH (MANUAL)',
          PaymentMethod.qrPh => 'QR PH',
          PaymentMethod.bankTransfer => 'BANK TRANSFER',
          PaymentMethod.utangCredit => 'CUSTOMER CREDIT',
          PaymentMethod.billPaymentELoad => 'E-LOAD',
          PaymentMethod.split => 'SPLIT PAYMENT',
        };

        if (payment.amountTendered != null) {
          builder.twoColumn(
            methodName,
            'PHP ${payment.amountTendered!.toStringAsFixed(2)}',
          );
        } else {
          builder.twoColumn(
            methodName,
            'PHP ${payment.amount.toStringAsFixed(2)}',
          );
        }

        if (payment.changeGiven != null && payment.changeGiven! > 0) {
          builder.twoColumn(
            'CHANGE',
            'PHP ${payment.changeGiven!.toStringAsFixed(2)}',
            isBold: true,
          );
        }
      }
      builder.divider('-');
    }

    // 8. BIR Buyer Details Section (Required for Tax Clearance)
    builder.text('Customer Name: _____________________');
    builder.text('Address:       _____________________');
    builder.text('TIN:           _____________________');
    builder.divider('-');

    // 9. Official BIR Disclaimer & Verification
    builder.align(PrintAlignment.center);
    builder.text('THIS SERVES AS AN OFFICIAL RECEIPT', isBold: true);
    builder.text('Thank you for your patronage!');

    if (customFooter != null && customFooter.isNotEmpty) {
      builder.text(customFooter);
    }

    builder.feedLines(1);
    builder.text('POS Provider: Purch.io Cloud & Edge');
    builder.text('Accreditation No: BIR-POS-2026-0916');

    // 10. Drawer Kick & Cut Paper
    if (kickDrawer) {
      builder.kickDrawer(pin5: pin5Drawer);
    }

    if (cutPaper) {
      builder.cut(feedLines: 3, partial: true);
    } else {
      builder.feedLines(3);
    }

    return builder.toBytes();
  }
}
