import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/kiosk/domain/kiosk_session_repository.dart';

class FakeKioskSessionRepository implements KioskSessionRepository {
  FakeKioskSessionRepository({this.failureToThrow});

  final Failure? failureToThrow;
  String? lastPairingCode;

  @override
  Future<void> pair({required String devicePairingCode}) async {
    lastPairingCode = devicePairingCode;
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
  }
}
