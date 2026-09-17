import 'printer/esc_pos_builder.dart';
import 'printer/printer_transport.dart';
import 'scale/scale_models.dart';

/// Persisted configuration for the workstation's physical & virtual peripherals.
class HardwareConfig {
  const HardwareConfig({
    // Thermal Printer (Defaults to mock for zero-network environments & tests)
    this.printerType = PrinterConnectionType.mock,
    this.printerHost = '192.168.1.200',
    this.printerPort = 9100,
    this.printerSerialPort = 'COM1',
    this.paperWidth = PaperWidth.mm80,
    this.autoCut = true,
    this.autoKickDrawerOnCash = true,

    // Scale
    this.scaleProtocol = ScaleProtocol.cas,
    this.scaleSimulator = true,
    this.scalePort = 'COM3',
    this.scaleBaudRate = 9600,

    // CFD & Pole Display
    this.cfdServerEnabled = false,
    this.cfdPort = 8088,
    this.enableVfdPole = false,
  });

  // Printer
  final PrinterConnectionType printerType;
  final String printerHost;
  final int printerPort;
  final String printerSerialPort;
  final PaperWidth paperWidth;
  final bool autoCut;
  final bool autoKickDrawerOnCash;

  // Scale
  final ScaleProtocol scaleProtocol;
  final bool scaleSimulator;
  final String scalePort;
  final int scaleBaudRate;

  // CFD & Pole
  final bool cfdServerEnabled;
  final int cfdPort;
  final bool enableVfdPole;

  Map<String, dynamic> toJson() => {
        'printerType': printerType.name,
        'printerHost': printerHost,
        'printerPort': printerPort,
        'printerSerialPort': printerSerialPort,
        'paperWidth': paperWidth.name,
        'autoCut': autoCut,
        'autoKickDrawerOnCash': autoKickDrawerOnCash,
        'scaleProtocol': scaleProtocol.name,
        'scaleSimulator': scaleSimulator,
        'scalePort': scalePort,
        'scaleBaudRate': scaleBaudRate,
        'cfdServerEnabled': cfdServerEnabled,
        'cfdPort': cfdPort,
        'enableVfdPole': enableVfdPole,
      };

  factory HardwareConfig.fromJson(Map<String, dynamic> json) => HardwareConfig(
        printerType: PrinterConnectionType.values.byName(
          json['printerType'] as String? ?? 'networkTcp',
        ),
        printerHost: json['printerHost'] as String? ?? '192.168.1.200',
        printerPort: (json['printerPort'] as num?)?.toInt() ?? 9100,
        printerSerialPort: json['printerSerialPort'] as String? ?? 'COM1',
        paperWidth: PaperWidth.values.byName(
          json['paperWidth'] as String? ?? 'mm80',
        ),
        autoCut: json['autoCut'] as bool? ?? true,
        autoKickDrawerOnCash: json['autoKickDrawerOnCash'] as bool? ?? true,
        scaleProtocol: ScaleProtocol.values.byName(
          json['scaleProtocol'] as String? ?? 'cas',
        ),
        scaleSimulator: json['scaleSimulator'] as bool? ?? true,
        scalePort: json['scalePort'] as String? ?? 'COM3',
        scaleBaudRate: (json['scaleBaudRate'] as num?)?.toInt() ?? 9600,
        cfdServerEnabled: json['cfdServerEnabled'] as bool? ?? true,
        cfdPort: (json['cfdPort'] as num?)?.toInt() ?? 8088,
        enableVfdPole: json['enableVfdPole'] as bool? ?? false,
      );

  HardwareConfig copyWith({
    PrinterConnectionType? printerType,
    String? printerHost,
    int? printerPort,
    String? printerSerialPort,
    PaperWidth? paperWidth,
    bool? autoCut,
    bool? autoKickDrawerOnCash,
    ScaleProtocol? scaleProtocol,
    bool? scaleSimulator,
    String? scalePort,
    int? scaleBaudRate,
    bool? cfdServerEnabled,
    int? cfdPort,
    bool? enableVfdPole,
  }) {
    return HardwareConfig(
      printerType: printerType ?? this.printerType,
      printerHost: printerHost ?? this.printerHost,
      printerPort: printerPort ?? this.printerPort,
      printerSerialPort: printerSerialPort ?? this.printerSerialPort,
      paperWidth: paperWidth ?? this.paperWidth,
      autoCut: autoCut ?? this.autoCut,
      autoKickDrawerOnCash: autoKickDrawerOnCash ?? this.autoKickDrawerOnCash,
      scaleProtocol: scaleProtocol ?? this.scaleProtocol,
      scaleSimulator: scaleSimulator ?? this.scaleSimulator,
      scalePort: scalePort ?? this.scalePort,
      scaleBaudRate: scaleBaudRate ?? this.scaleBaudRate,
      cfdServerEnabled: cfdServerEnabled ?? this.cfdServerEnabled,
      cfdPort: cfdPort ?? this.cfdPort,
      enableVfdPole: enableVfdPole ?? this.enableVfdPole,
    );
  }
}
