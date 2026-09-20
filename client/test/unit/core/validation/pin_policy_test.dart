import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/validation/pin_policy.dart';

void main() {
  test('accepts 4 to 8 digits', () {
    for (final pin in ['1234', '123456', '12345678', ' 4321 ']) {
      expect(validatePin(pin), isNull, reason: pin);
    }
  });

  test('rejects blank, too short, too long and non-digit PINs', () {
    expect(validatePin(null), 'Required');
    expect(validatePin('   '), 'Required');
    for (final pin in ['12', '123', '123456789', '12a4', '12 34', '١٢٣٤']) {
      expect(validatePin(pin), isNotNull, reason: pin);
    }
  });
}
