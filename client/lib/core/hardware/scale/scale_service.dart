import 'dart:async';
import 'scale_driver.dart';
import 'scale_models.dart';

class ScaleService {
  ScaleService({ScaleDriver? driver})
      : _driver = driver ?? MockScaleDriver(initialWeight: 0.0) {
    _driver.connect();
  }

  ScaleDriver _driver;

  ScaleDriver get driver => _driver;
  Stream<ScaleReading> get stream => _driver.stream;
  ScaleReading get latestReading => _driver.latestReading;
  ScaleStatus get status => _driver.status;
  ScaleProtocol get protocol => _driver.protocol;

  void updateDriver(ScaleDriver newDriver) {
    _driver.disconnect();
    _driver = newDriver;
    _driver.connect();
  }

  Future<void> zero() => _driver.zero();
  Future<void> tare() => _driver.tare();

  void dispose() {
    _driver.dispose();
  }
}
