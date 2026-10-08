/// What a staff PIN may look like — mirrors backend PinPolicy (6-8 digits for a PIN being set; older 4-digit PINs still sign in), so a bad PIN is caught on the
/// form instead of coming back from the server as a 400.
const pinMinLength = 6;
const pinMaxLength = 8;

/// A form-field validator: null when [value] is an acceptable PIN.
String? validatePin(String? value) {
  final pin = value?.trim() ?? '';
  if (pin.isEmpty) {
    return 'Required';
  }
  if (pin.length < pinMinLength ||
      pin.length > pinMaxLength ||
      !RegExp(r'^\d+$').hasMatch(pin)) {
    return 'Use $pinMinLength-$pinMaxLength digits';
  }
  return null;
}
