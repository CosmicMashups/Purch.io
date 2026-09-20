/// What a staff PIN may look like — mirrors backend PinPolicy (4-8 digits), so a bad PIN is caught on the
/// form instead of coming back from the server as a 400.
const pinMinLength = 4;
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
