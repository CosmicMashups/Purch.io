import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'scale_models.dart';
import 'scale_parser.dart';

abstract class ScaleDriver {
  Stream<ScaleReading> get stream;
  ScaleReading get latestReading;
  ScaleStatus get status;
  ScaleProtocol get protocol;

  Future<void> connect();
  Future<void> disconnect();
  Future<void> zero();
  Future<void> tare();
  void dispose();
}

/// Serial RS232 / USB / TCP-to-Serial scale driver.
class SerialScaleDriver implements ScaleDriver {
  SerialScaleDriver({
    required this.portOrHost,
    this.baudRate = 9600,
    this.protocol = ScaleProtocol.cas,
    this.isTcp = false,
    this.tcpPort = 4001,
  });

  final String portOrHost;
  final int baudRate;
  @override
  final ScaleProtocol protocol;
  final bool isTcp;
  final int tcpPort;

  final _controller = StreamController<ScaleReading>.broadcast();
  ScaleReading _latest = ScaleReading(
    weight: 0.0,
    unit: 'kg',
    isStable: false,
    timestamp: DateTime.now(),
  );
  ScaleStatus _status = ScaleStatus.disconnected;

  Socket? _tcpSocket;
  StreamSubscription<List<int>>? _subscription;
  final StringBuffer _buffer = StringBuffer();

  @override
  Stream<ScaleReading> get stream => _controller.stream;

  @override
  ScaleReading get latestReading => _latest;

  @override
  ScaleStatus get status => _status;

  @override
  Future<void> connect() async {
    _status = ScaleStatus.connecting;
    try {
      if (isTcp) {
        _tcpSocket = await Socket.connect(portOrHost, tcpPort, timeout: const Duration(seconds: 3));
        _subscription = _tcpSocket!.listen(
          _onDataBytes,
          onError: (e) {
            _status = ScaleStatus.error;
          },
          onDone: () {
            _status = ScaleStatus.disconnected;
          },
        );
      }
      _status = ScaleStatus.connected;
    } catch (_) {
      _status = ScaleStatus.error;
    }
  }

  void _onDataBytes(List<int> bytes) {
    final text = utf8.decode(bytes, allowMalformed: true);
    _buffer.write(text);

    final content = _buffer.toString();
    final lines = content.split(RegExp(r'[\r\n]+'));

    // Keep the last incomplete fragment in buffer
    if (lines.isNotEmpty) {
      _buffer.clear();
      _buffer.write(lines.last);

      for (var i = 0; i < lines.length - 1; i++) {
        final line = lines[i].trim();
        if (line.isEmpty) continue;

        final reading = protocol == ScaleProtocol.cas
            ? ScaleParser.parseCas(line)
            : ScaleParser.parseMettlerToledo(line);

        if (reading != null) {
          _latest = reading;
          _controller.add(reading);
        }
      }
    }
  }

  @override
  Future<void> zero() async {
    if (_tcpSocket != null) {
      _tcpSocket!.write('Z\r\n');
      await _tcpSocket!.flush();
    }
  }

  @override
  Future<void> tare() async {
    if (_tcpSocket != null) {
      _tcpSocket!.write('T\r\n');
      await _tcpSocket!.flush();
    }
  }

  @override
  Future<void> disconnect() async {
    await _subscription?.cancel();
    await _tcpSocket?.close();
    _subscription = null;
    _tcpSocket = null;
    _status = ScaleStatus.disconnected;
  }

  @override
  void dispose() {
    disconnect();
    _controller.close();
  }
}

/// High-fidelity Mock Scale Driver with live stream simulation for development,
/// testing, and branches without physical RS232 COM ports.
class MockScaleDriver implements ScaleDriver {
  MockScaleDriver({
    double initialWeight = 0.0,
    this.protocol = ScaleProtocol.cas,
    bool autoEmit = true,
  }) : _currentWeight = initialWeight {
    _latest = ScaleReading(
      weight: initialWeight,
      unit: 'kg',
      isStable: true,
      isZero: initialWeight == 0.0,
      timestamp: DateTime.now(),
    );

    if (autoEmit) {
      _timer = Timer.periodic(const Duration(milliseconds: 250), (_) {
        if (_status == ScaleStatus.connected) {
          _controller.add(_latest);
        }
      });
    }
  }

  @override
  final ScaleProtocol protocol;
  final _controller = StreamController<ScaleReading>.broadcast();
  Timer? _timer;

  double _currentWeight = 0.0;
  double _tareWeight = 0.0;
  bool _isStable = true;
  ScaleStatus _status = ScaleStatus.connected;
  late ScaleReading _latest;

  @override
  Stream<ScaleReading> get stream => _controller.stream;

  @override
  ScaleReading get latestReading => _latest;

  @override
  ScaleStatus get status => _status;

  /// Programmatically set weight & stability for testing or live simulation.
  void setSimulatedWeight(double weight, {bool isStable = true}) {
    _currentWeight = weight;
    _isStable = isStable;
    _emitCurrent();
  }

  void _emitCurrent() {
    final netWeight = (_currentWeight - _tareWeight).clamp(0.0, 999.0);
    _latest = ScaleReading(
      weight: netWeight,
      unit: 'kg',
      isStable: _isStable,
      isZero: netWeight.abs() < 0.001,
      isTare: _tareWeight > 0,
      isOverload: _currentWeight > 30.0, // 30kg typical retail scale limit
      timestamp: DateTime.now(),
      rawData: 'SIMULATED:${netWeight.toStringAsFixed(3)}kg',
    );
    _controller.add(_latest);
  }

  @override
  Future<void> connect() async {
    _status = ScaleStatus.connected;
    _emitCurrent();
  }

  @override
  Future<void> zero() async {
    _currentWeight = 0.0;
    _tareWeight = 0.0;
    _isStable = true;
    _emitCurrent();
  }

  @override
  Future<void> tare() async {
    _tareWeight = _currentWeight;
    _isStable = true;
    _emitCurrent();
  }

  @override
  Future<void> disconnect() async {
    _status = ScaleStatus.disconnected;
  }

  @override
  void dispose() {
    _timer?.cancel();
    _controller.close();
  }
}
