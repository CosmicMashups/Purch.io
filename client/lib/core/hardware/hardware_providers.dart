import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'cfd/cfd_models.dart';
import 'cfd/cfd_service.dart';
import 'drawer/cash_drawer_service.dart';
import 'hardware_config.dart';
import 'hardware_config_storage.dart';
import 'printer/printer_service.dart';
import 'printer/printer_transport.dart';
import 'scale/scale_driver.dart';
import 'scale/scale_models.dart';
import 'scale/scale_service.dart';
import '../../features/onboarding/domain/hardware_enums.dart';

final hardwareConfigStorageProvider = Provider<HardwareConfigStorage>((ref) {
  return HardwareConfigStorage();
});

/// Manages workstation hardware configuration state and storage.
final hardwareConfigProvider =
    StateNotifierProvider<HardwareConfigNotifier, HardwareConfig>((ref) {
  final storage = ref.watch(hardwareConfigStorageProvider);
  return HardwareConfigNotifier(storage);
});

class HardwareConfigNotifier extends StateNotifier<HardwareConfig> {
  HardwareConfigNotifier(this._storage) : super(const HardwareConfig()) {
    _load();
  }

  final HardwareConfigStorage _storage;

  Future<void> _load() async {
    final loaded = await _storage.loadConfig();
    state = loaded;
  }

  Future<void> updateConfig(HardwareConfig newConfig) async {
    state = newConfig;
    await _storage.saveConfig(newConfig);
  }
}

/// Printer service provider with connection transport matching the active config.
final printerServiceProvider = Provider<PrinterService>((ref) {
  final config = ref.watch(hardwareConfigProvider);

  PrinterTransport transport;
  switch (config.printerType) {
    case PrinterConnectionType.networkTcp:
      transport = NetworkPrinterTransport(
        host: config.printerHost,
        port: config.printerPort,
      );
      break;
    case PrinterConnectionType.usbSerial:
      transport = SerialUsbPrinterTransport(
        portName: config.printerSerialPort,
      );
      break;
    case PrinterConnectionType.mock:
    case PrinterConnectionType.bluetooth:
      transport = MockPrinterTransport();
      break;
  }

  final service = PrinterService(
    transport: transport,
    defaultPaperWidth: config.paperWidth,
  );

  ref.onDispose(service.dispose);
  return service;
});

/// Cash drawer service provider configured with the thermal printer transport.
final cashDrawerServiceProvider = Provider<CashDrawerService>((ref) {
  final printer = ref.watch(printerServiceProvider);
  final config = ref.watch(hardwareConfigProvider);

  return CashDrawerService(
    printerService: printer,
    enabled: config.autoKickDrawerOnCash,
    defaultPolicy: CashDrawerPolicy.allowManualOpenWithManagerOverride,
  );
});

/// Scale service provider with physical or simulated scale driver.
final scaleServiceProvider = Provider<ScaleService>((ref) {
  final config = ref.watch(hardwareConfigProvider);

  ScaleDriver driver;
  if (config.scaleSimulator) {
    driver = MockScaleDriver(
      protocol: config.scaleProtocol,
      initialWeight: 0.0,
      autoEmit: true,
    );
  } else {
    driver = SerialScaleDriver(
      portOrHost: config.scalePort,
      baudRate: config.scaleBaudRate,
      protocol: config.scaleProtocol,
    );
  }

  final service = ScaleService(driver: driver);
  ref.onDispose(service.dispose);
  return service;
});

/// Stream of live weight readings from the active scale.
final scaleReadingStreamProvider = StreamProvider.autoDispose<ScaleReading>((ref) {
  final scaleService = ref.watch(scaleServiceProvider);
  return scaleService.stream;
});

/// Customer Facing Display service provider.
final cfdServiceProvider = Provider<CfdService>((ref) {
  final config = ref.watch(hardwareConfigProvider);

  final service = CfdService(
    autoStartServer: config.cfdServerEnabled,
  );

  ref.onDispose(service.dispose);
  return service;
});

/// Stream of CFD state updates for secondary displays.
final cfdStateStreamProvider = StreamProvider.autoDispose<CfdState>((ref) {
  final cfdService = ref.watch(cfdServiceProvider);
  return cfdService.stateStream;
});
