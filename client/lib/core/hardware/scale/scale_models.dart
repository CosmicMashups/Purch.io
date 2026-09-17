/// Protocols supported for physical RS232 / USB scales.
enum ScaleProtocol {
  /// CAS AP-1 / ER Plus / SW-1 / PD-II protocol.
  cas,

  /// Mettler-Toledo MT-SICS and Toledo 8217 protocol.
  mettlerToledo,
}

enum ScaleStatus { disconnected, connecting, connected, error }

/// Represents a single parsed reading from a physical scale.
class ScaleReading {
  const ScaleReading({
    required this.weight,
    this.unit = 'kg',
    required this.isStable,
    this.isZero = false,
    this.isOverload = false,
    this.isTare = false,
    required this.timestamp,
    this.rawData = '',
  });

  /// The measured net weight in [unit].
  final double weight;

  /// Unit of measurement: 'kg', 'g', 'lb'.
  final String unit;

  /// Crucial for retail/palengke: true when the weight has settled and is stable.
  /// Transactions should only lock in weight when this is true to prevent tampering.
  final bool isStable;

  /// True when the scale platter is empty/zeroed.
  final bool isZero;

  /// True when the placed load exceeds the scale capacity.
  final bool isOverload;

  /// True when a tare container weight has been subtracted.
  final bool isTare;

  final DateTime timestamp;
  final String rawData;

  @override
  String toString() =>
      'ScaleReading(${weight.toStringAsFixed(3)} $unit, stable: $isStable, zero: $isZero)';
}
