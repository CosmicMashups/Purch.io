import 'scale_models.dart';

/// Decodes raw ASCII / serial byte streams from CAS and Mettler-Toledo scales.
class ScaleParser {
  const ScaleParser._();

  /// Parses a complete line/frame from a CAS scale.
  /// Example: "ST,GS,+  1.250kg\r\n" or "US,GS,+  0.850kg" or "OL,GS,..."
  static ScaleReading? parseCas(String rawLine) {
    final cleaned = rawLine.replaceAll(RegExp(r'[\x00-\x1F]'), ' ').trim();
    if (cleaned.isEmpty) return null;

    final parts = cleaned.split(',');
    if (parts.length >= 2) {
      final header1 = parts[0].trim().toUpperCase();
      final isStable = header1 == 'ST';
      final isOverload = header1 == 'OL';

      // Find the segment containing numbers and unit
      String dataSegment = parts.length >= 3 ? parts[2] : parts[1];
      bool isTare = false;
      if (parts.length >= 3 && parts[1].trim().toUpperCase() == 'NT') {
        isTare = true;
      }

      if (isOverload) {
        return ScaleReading(
          weight: 0.0,
          unit: 'kg',
          isStable: false,
          isOverload: true,
          timestamp: DateTime.now(),
          rawData: rawLine,
        );
      }

      // Extract sign and weight digits
      final match = RegExp(r'([+-]?\s*\d+\.?\d*)\s*([a-zA-Z]+)?').firstMatch(dataSegment);
      if (match != null) {
        final weightStr = match.group(1)?.replaceAll(' ', '') ?? '0';
        final unitStr = match.group(2)?.toLowerCase() ?? 'kg';
        final weight = double.tryParse(weightStr) ?? 0.0;

        return ScaleReading(
          weight: weight,
          unit: unitStr,
          isStable: isStable,
          isZero: weight.abs() < 0.001,
          isOverload: false,
          isTare: isTare,
          timestamp: DateTime.now(),
          rawData: rawLine,
        );
      }
    }

    // Fallback simple numeric regex
    return _regexFallback(cleaned, rawLine, defaultStable: false);
  }

  /// Parses a line from a Mettler-Toledo scale (MT-SICS format or Toledo continuous).
  /// Example MT-SICS: "S S      1.250 kg" (Stable) or "S D      1.250 kg" (Dynamic/Motion)
  static ScaleReading? parseMettlerToledo(String rawLine) {
    final cleaned = rawLine.replaceAll(RegExp(r'[\x00-\x1F]'), ' ').trim();
    if (cleaned.isEmpty) return null;

    // Overload / Underload
    if (cleaned.contains('S +') || cleaned.contains('OL')) {
      return ScaleReading(
        weight: 0.0,
        unit: 'kg',
        isStable: false,
        isOverload: true,
        timestamp: DateTime.now(),
        rawData: rawLine,
      );
    }

    // MT-SICS standard response: "S S <weight> <unit>" or "S D <weight> <unit>"
    final sicsMatch = RegExp(
      r'S\s+([SDI])\s+([+-]?\d+\.?\d*)\s*([a-zA-Z]+)?',
      caseSensitive: false,
    ).firstMatch(cleaned);

    if (sicsMatch != null) {
      final statusChar = sicsMatch.group(1)!.toUpperCase();
      final isStable = statusChar == 'S';
      final weightStr = sicsMatch.group(2) ?? '0';
      final unitStr = sicsMatch.group(3)?.toLowerCase() ?? 'kg';
      final weight = double.tryParse(weightStr) ?? 0.0;

      return ScaleReading(
        weight: weight,
        unit: unitStr,
        isStable: isStable,
        isZero: weight.abs() < 0.001,
        isOverload: false,
        timestamp: DateTime.now(),
        rawData: rawLine,
      );
    }

    // Toledo 8217 continuous format fallback
    return _regexFallback(cleaned, rawLine, defaultStable: true);
  }

  static ScaleReading? _regexFallback(
    String cleaned,
    String rawLine, {
    required bool defaultStable,
  }) {
    final match = RegExp(r'([+-]?\d+\.?\d*)\s*(kg|g|lb)?', caseSensitive: false)
        .firstMatch(cleaned);
    if (match != null) {
      final weight = double.tryParse(match.group(1)!) ?? 0.0;
      final unit = match.group(2)?.toLowerCase() ?? 'kg';
      return ScaleReading(
        weight: weight,
        unit: unit,
        isStable: defaultStable,
        isZero: weight.abs() < 0.001,
        timestamp: DateTime.now(),
        rawData: rawLine,
      );
    }
    return null;
  }
}
