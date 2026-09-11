import 'shift_models.dart';

/// D7 — opening/closing a device's cash-drawer session.
abstract class ShiftRepository {
  /// Null when the current device has no shift open right now.
  Future<Shift?> getCurrentShift();

  Future<Shift> openShift(OpenShiftRequest request);

  Future<Shift> closeShift(CloseShiftRequest request);
}
