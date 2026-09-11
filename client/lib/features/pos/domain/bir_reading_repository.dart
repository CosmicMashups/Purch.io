import 'bir_reading_models.dart';

/// F2/FR26 — BIR X-reading (mid-shift, re-runnable) / Z-reading
/// (end-of-day, advances the reset counter) report generation.
abstract class BirReadingRepository {
  Future<BirReading> generateXReading();

  Future<BirReading> generateZReading();
}
