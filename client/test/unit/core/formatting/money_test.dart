import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/formatting/money.dart';

void main() {
  test('formatCurrency shows two decimals and thousands separators', () {
    expect(formatCurrency(1234.5), '₱1,234.50');
    expect(formatCurrency(0), '₱0.00');
  });

  test('formatCurrencyAbs drops the sign', () {
    expect(formatCurrencyAbs(-50), '₱50.00');
  });

  test('formatCurrencyCompact abbreviates thousands and millions', () {
    expect(formatCurrencyCompact(500), '₱500');
    expect(formatCurrencyCompact(12432), '₱12k');
    expect(formatCurrencyCompact(9500), '₱9.5k');
    expect(formatCurrencyCompact(2500000), '₱2.5M');
    expect(formatCurrencyCompact(-15000), '-₱15k');
  });
}
