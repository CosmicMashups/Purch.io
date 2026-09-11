import 'package:purch_client/features/pos/domain/shift_models.dart';
import 'package:purch_client/features/pos/domain/shift_repository.dart';

class FakeShiftRepository implements ShiftRepository {
  FakeShiftRepository({
    this.openShiftFailure,
    this.closeShiftFailure,
    Shift? initialShift,
  }) : shift = initialShift;

  final Object? openShiftFailure;
  final Object? closeShiftFailure;

  Shift? shift;
  OpenShiftRequest? lastOpenShiftRequest;
  CloseShiftRequest? lastCloseShiftRequest;

  @override
  Future<Shift?> getCurrentShift() async =>
      shift?.status == ShiftStatus.open ? shift : null;

  @override
  Future<Shift> openShift(OpenShiftRequest request) async {
    lastOpenShiftRequest = request;
    if (openShiftFailure != null) {
      throw openShiftFailure!;
    }
    final opened = Shift(
      id: 'shift-1',
      branchId: 'branch-1',
      deviceId: 'device-1',
      status: ShiftStatus.open,
      openedByUserId: 'user-1',
      openedByUserName: 'Admin User',
      openingCashAmount: request.openingCashAmount,
      openedAt: DateTime(2026, 1, 1),
      closedByUserId: null,
      closedByUserName: null,
      closingCashAmount: null,
      expectedCashAmount: null,
      varianceAmount: null,
      handoverNotes: null,
      approvedByUserId: null,
      approvedByUserName: null,
      closedAt: null,
    );
    shift = opened;
    return opened;
  }

  @override
  Future<Shift> closeShift(CloseShiftRequest request) async {
    lastCloseShiftRequest = request;
    if (closeShiftFailure != null) {
      throw closeShiftFailure!;
    }
    final current = shift!;
    final expected = current.openingCashAmount;
    final variance = request.closingCashAmount - expected;
    final closed = Shift(
      id: current.id,
      branchId: current.branchId,
      deviceId: current.deviceId,
      status: ShiftStatus.closed,
      openedByUserId: current.openedByUserId,
      openedByUserName: current.openedByUserName,
      openingCashAmount: current.openingCashAmount,
      openedAt: current.openedAt,
      closedByUserId: 'user-1',
      closedByUserName: 'Admin User',
      closingCashAmount: request.closingCashAmount,
      expectedCashAmount: expected,
      varianceAmount: variance,
      handoverNotes: request.handoverNotes,
      approvedByUserId: variance != 0 ? 'user-2' : null,
      approvedByUserName: variance != 0 ? 'Manager Mae' : null,
      closedAt: DateTime(2026, 1, 1, 8),
    );
    shift = closed;
    return closed;
  }
}
