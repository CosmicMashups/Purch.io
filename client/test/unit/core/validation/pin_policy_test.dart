import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/validation/pin_policy.dart';

void main() {
  test('accepts 6 to 8 digits for a PIN being set', () {
    for (final pin in ['123456', '1234567', '12345678', ' 432112 ']) {
      expect(validatePin(pin), isNull, reason: pin);
    }
  });

  test('rejects blank, too short, too long and non-digit PINs', () {
    expect(validatePin(null), 'Required');
    expect(validatePin('   '), 'Required');
    // 4 and 5 digits were valid before; they can no longer be chosen (existing ones still sign in).
    for (final pin in ['12', '1234', '12345', '123456789', '12a456', '12 3456', '١٢٣٤٥٦']) {
      expect(validatePin(pin), isNotNull, reason: pin);
    }
  });
}
