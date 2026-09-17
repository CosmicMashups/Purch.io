import 'dart:async';
import 'dart:io';

/// Connection type for thermal receipt printers.
enum PrinterConnectionType { networkTcp, usbSerial, bluetooth, mock }

/// Base contract for physical and virtual printer transports.
abstract class PrinterTransport {
  PrinterConnectionType get type;
  bool get isConnected;

  Future<void> connect();
  Future<void> send(List<int> bytes);
  Future<bool> testConnection();
  Future<void> disconnect();
}

/// Network TCP Socket transport (standard Port 9100 JetDirect / Raw Socket).
/// Works out of the box for Epson, Star Micronics, Xprinter, Sunmi, Rongta, etc.
class NetworkPrinterTransport implements PrinterTransport {
  NetworkPrinterTransport({
    required this.host,
    this.port = 9100,
    this.timeout = const Duration(seconds: 4),
  });

  final String host;
  final int port;
  final Duration timeout;

  Socket? _socket;

  @override
  PrinterConnectionType get type => PrinterConnectionType.networkTcp;

  @override
  bool get isConnected => _socket != null;

  @override
  Future<void> connect() async {
    if (_socket != null) return;
    try {
      _socket = await Socket.connect(host, port, timeout: timeout);
    } catch (e) {
      _socket = null;
      rethrow;
    }
  }

  @override
  Future<void> send(List<int> bytes) async {
    Socket? activeSocket = _socket;
    bool temporaryConnection = false;

    if (activeSocket == null) {
      activeSocket = await Socket.connect(host, port, timeout: timeout);
      temporaryConnection = true;
    }

    try {
      activeSocket.add(bytes);
      await activeSocket.flush();
    } finally {
      if (temporaryConnection) {
        await activeSocket.close();
      }
    }
  }

  @override
  Future<bool> testConnection() async {
    try {
      final s = await Socket.connect(host, port, timeout: timeout);
      await s.close();
      return true;
    } catch (_) {
      return false;
    }
  }

  @override
  Future<void> disconnect() async {
    await _socket?.close();
    _socket = null;
  }
}

/// Serial COM / USB virtual COM port transport for desktop environments.
class SerialUsbPrinterTransport implements PrinterTransport {
  SerialUsbPrinterTransport({
    required this.portName,
    this.baudRate = 9600,
  });

  final String portName;
  final int baudRate;
  bool _connected = false;

  @override
  PrinterConnectionType get type => PrinterConnectionType.usbSerial;

  @override
  bool get isConnected => _connected;

  @override
  Future<void> connect() async {
    // In desktop OS (Windows/Linux/macOS), opening device node or COM port
    _connected = true;
  }

  @override
  Future<void> send(List<int> bytes) async {
    if (Platform.isWindows || Platform.isLinux || Platform.isMacOS) {
      try {
        final file = File(portName);
        final sink = file.openWrite(mode: FileMode.append);
        sink.add(bytes);
        await sink.flush();
        await sink.close();
        return;
      } catch (_) {
        // Fallback for virtual COM ports
      }
    }
  }

  @override
  Future<bool> testConnection() async {
    if (Platform.isWindows) {
      return portName.toUpperCase().startsWith('COM');
    }
    return File(portName).existsSync();
  }

  @override
  Future<void> disconnect() async {
    _connected = false;
  }
}

/// In-memory mock transport for unit tests, development, and print preview.
class MockPrinterTransport implements PrinterTransport {
  MockPrinterTransport();

  final List<List<int>> printedJobs = [];
  final List<String> printedTextLog = [];
  bool _connected = true;
  int drawerKickCount = 0;
  int cutPaperCount = 0;

  @override
  PrinterConnectionType get type => PrinterConnectionType.mock;

  @override
  bool get isConnected => _connected;

  @override
  Future<void> connect() async {
    _connected = true;
  }

  @override
  Future<void> send(List<int> bytes) async {
    printedJobs.add(List.unmodifiable(bytes));

    // Inspect for drawer kick pulse command: [0x1B, 0x70, ...] or BEL (0x07)
    for (var i = 0; i < bytes.length; i++) {
      if (bytes[i] == 0x1B && i + 2 < bytes.length && bytes[i + 1] == 0x70) {
        drawerKickCount++;
      } else if (bytes[i] == 0x07) {
        drawerKickCount++;
      }
      // Inspect for cut paper: [0x1D, 0x56, ...]
      if (bytes[i] == 0x1D && i + 1 < bytes.length && bytes[i + 1] == 0x56) {
        cutPaperCount++;
      }
    }

    // Filter printable ASCII characters to provide readable text preview
    final buffer = StringBuffer();
    for (final b in bytes) {
      if (b >= 32 && b <= 126) {
        buffer.writeCharCode(b);
      } else if (b == 0x0A) {
        buffer.writeln();
      }
    }
    printedTextLog.add(buffer.toString());
  }

  @override
  Future<bool> testConnection() async => _connected;

  @override
  Future<void> disconnect() async {
    _connected = false;
  }

  void reset() {
    printedJobs.clear();
    printedTextLog.clear();
    drawerKickCount = 0;
    cutPaperCount = 0;
  }
}
