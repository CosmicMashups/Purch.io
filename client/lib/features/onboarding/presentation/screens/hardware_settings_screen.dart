import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_fonts/google_fonts.dart';

import '../../../../core/hardware/hardware_providers.dart';
import '../../../../core/hardware/printer/esc_pos_builder.dart';
import '../../../../core/hardware/printer/printer_transport.dart';
import '../../../../core/hardware/scale/scale_models.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/hardware/cfd/customer_facing_display_screen.dart';

/// Workstation & Branch Hardware Ecosystem Management Screen.
/// Manages Direct ESC/POS Thermal Printers, Cash Drawer RJ11 Kick Pulses,
/// RS232 / USB Scales (CAS / Mettler-Toledo), and Customer Facing Displays.
class HardwareSettingsScreen extends ConsumerStatefulWidget {
  const HardwareSettingsScreen({super.key, this.branchId});

  final String? branchId;

  @override
  ConsumerState<HardwareSettingsScreen> createState() => _HardwareSettingsScreenState();
}

class _HardwareSettingsScreenState extends ConsumerState<HardwareSettingsScreen> {
  late TextEditingController _printerHostController;
  late TextEditingController _printerPortController;
  late TextEditingController _printerSerialController;
  late TextEditingController _scalePortController;
  late TextEditingController _cfdPortController;

  bool _initialized = false;
  bool _testingPrint = false;
  bool _testingKick = false;
  String? _lanAddress;

  Future<void> _resolveLanAddress() async {
    try {
      final interfaces = await NetworkInterface.list(
        type: InternetAddressType.IPv4,
        includeLoopback: false,
        includeLinkLocal: false,
      );
      for (final interface in interfaces) {
        for (final addr in interface.addresses) {
          if (!addr.isLoopback) {
            if (mounted) setState(() => _lanAddress = addr.address);
            return;
          }
        }
      }
    } catch (_) {
      // Leave _lanAddress null; UI falls back to "this device's LAN IP".
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_initialized) {
      final config = ref.read(hardwareConfigProvider);
      _printerHostController = TextEditingController(text: config.printerHost);
      _printerPortController = TextEditingController(text: config.printerPort.toString());
      _printerSerialController = TextEditingController(text: config.printerSerialPort);
      _scalePortController = TextEditingController(text: config.scalePort);
      _cfdPortController = TextEditingController(text: config.cfdPort.toString());
      _initialized = true;
      _resolveLanAddress();
    }
  }

  @override
  void dispose() {
    _printerHostController.dispose();
    _printerPortController.dispose();
    _printerSerialController.dispose();
    _scalePortController.dispose();
    _cfdPortController.dispose();
    super.dispose();
  }

  void _saveConfig() {
    final current = ref.read(hardwareConfigProvider);
    final updated = current.copyWith(
      printerHost: _printerHostController.text.trim(),
      printerPort: int.tryParse(_printerPortController.text) ?? 9100,
      printerSerialPort: _printerSerialController.text.trim(),
      scalePort: _scalePortController.text.trim(),
      cfdPort: int.tryParse(_cfdPortController.text) ?? 8088,
    );
    ref.read(hardwareConfigProvider.notifier).updateConfig(updated);

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Hardware settings saved.')),
    );
  }

  Future<void> _testPrint() async {
    setState(() => _testingPrint = true);
    final printer = ref.read(printerServiceProvider);
    final success = await printer.printTestPage();
    if (mounted) {
      setState(() => _testingPrint = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(success ? 'Test page sent to printer.' : 'Printer communication error.'),
          backgroundColor: success ? AppColors.accentEmerald : Colors.red,
        ),
      );
    }
  }

  Future<void> _testKickDrawer() async {
    setState(() => _testingKick = true);
    final drawer = ref.read(cashDrawerServiceProvider);
    try {
      final success = await drawer.openManual(
        operatorName: 'Hardware Test',
        reason: 'Diagnostic Drawer Test',
        isManagerOverride: true,
      );
      if (mounted) {
        setState(() => _testingKick = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(success ? 'Cash drawer 24V pulse sent!' : 'Drawer kick failed.'),
            backgroundColor: success ? AppColors.accentEmerald : Colors.red,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        setState(() => _testingKick = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Drawer kick error: $e'), backgroundColor: Colors.red),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final config = ref.watch(hardwareConfigProvider);
    final scaleReading = ref.watch(scaleReadingStreamProvider).valueOrNull ??
        ref.read(scaleServiceProvider).latestReading;
    final cfdService = ref.read(cfdServiceProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Hardware & Peripheral Settings'),
        actions: [
          FilledButton.icon(
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.brandPrimary,
              foregroundColor: Colors.white,
            ),
            onPressed: _saveConfig,
            icon: const Icon(Icons.save_rounded, size: 18),
            label: const Text('Save'),
          ),
          const SizedBox(width: 16),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 20),
        children: [
          // ==========================================
          // 1. THERMAL RECEIPT PRINTER
          // ==========================================
          _buildCard(
            title: 'Direct ESC/POS Thermal Printer',
            icon: Icons.print_rounded,
            description:
                'Direct byte-stream printing to 58mm/80mm Epson, Star Micronics, Xprinter, and Sunmi devices.',
            children: [
              // Connection Type
              Row(
                children: [
                  const Expanded(
                    flex: 3,
                    child: Text(
                      'Connection Interface',
                      style: TextStyle(fontWeight: FontWeight.w600),
                    ),
                  ),
                  Expanded(
                    flex: 5,
                    child: DropdownButtonFormField<PrinterConnectionType>(
                      value: config.printerType,
                      decoration: const InputDecoration(border: OutlineInputBorder()),
                      items: const [
                        DropdownMenuItem(
                          value: PrinterConnectionType.networkTcp,
                          child: Text('LAN / Wi-Fi Network Socket (Raw 9100)'),
                        ),
                        DropdownMenuItem(
                          value: PrinterConnectionType.usbSerial,
                          child: Text('USB Virtual COM / Serial Port'),
                        ),
                        DropdownMenuItem(
                          value: PrinterConnectionType.bluetooth,
                          child: Text('Bluetooth Thermal Printer'),
                        ),
                        DropdownMenuItem(
                          value: PrinterConnectionType.mock,
                          child: Text('Simulated / Virtual Printer'),
                        ),
                      ],
                      onChanged: (v) {
                        if (v != null) {
                          ref.read(hardwareConfigProvider.notifier).updateConfig(
                                config.copyWith(printerType: v),
                              );
                        }
                      },
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),

              // Network IP and Port (if network)
              if (config.printerType == PrinterConnectionType.networkTcp) ...[
                Row(
                  children: [
                    Expanded(
                      flex: 3,
                      child: TextField(
                        controller: _printerHostController,
                        decoration: const InputDecoration(
                          labelText: 'Printer IP Address',
                          hintText: '192.168.1.200',
                          border: OutlineInputBorder(),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      flex: 1,
                      child: TextField(
                        controller: _printerPortController,
                        keyboardType: TextInputType.number,
                        decoration: const InputDecoration(
                          labelText: 'Port',
                          hintText: '9100',
                          border: OutlineInputBorder(),
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
              ],

              // Serial Port (if USB/Serial)
              if (config.printerType == PrinterConnectionType.usbSerial) ...[
                TextField(
                  controller: _printerSerialController,
                  decoration: const InputDecoration(
                    labelText: 'Serial Port Name',
                    hintText: 'COM1 or /dev/ttyUSB0',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 16),
              ],

              // Paper Width
              Row(
                children: [
                  const Expanded(
                    flex: 3,
                    child: Text(
                      'Paper Roll Width',
                      style: TextStyle(fontWeight: FontWeight.w600),
                    ),
                  ),
                  Expanded(
                    flex: 5,
                    child: SegmentedButton<PaperWidth>(
                      segments: const [
                        ButtonSegment(
                          value: PaperWidth.mm80,
                          label: Text('80mm (48 Cols)'),
                          icon: Icon(Icons.receipt_long),
                        ),
                        ButtonSegment(
                          value: PaperWidth.mm58,
                          label: Text('58mm (32 Cols)'),
                          icon: Icon(Icons.receipt),
                        ),
                      ],
                      selected: {config.paperWidth},
                      onSelectionChanged: (s) {
                        ref.read(hardwareConfigProvider.notifier).updateConfig(
                              config.copyWith(paperWidth: s.first),
                            );
                      },
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),

              // Auto-cut toggle
              SwitchListTile.adaptive(
                contentPadding: EdgeInsets.zero,
                title: const Text('Automatic Paper Cut (Partial Cut)'),
                subtitle: const Text('Sends GS V partial cut command after each receipt'),
                value: config.autoCut,
                activeColor: AppColors.brandPrimary,
                onChanged: (v) {
                  ref.read(hardwareConfigProvider.notifier).updateConfig(
                        config.copyWith(autoCut: v),
                      );
                },
              ),

              const SizedBox(height: 12),
              // Diagnostic Buttons
              Row(
                children: [
                  OutlinedButton.icon(
                    onPressed: _testingPrint ? null : _testPrint,
                    icon: _testingPrint
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.print_outlined),
                    label: const Text('Print Test Slip'),
                  ),
                ],
              ),
            ],
          ),

          const SizedBox(height: 24),

          // ==========================================
          // 2. CASH DRAWER RJ11 KICK
          // ==========================================
          _buildCard(
            title: 'Cash Drawer RJ11 Kick Pulse',
            icon: Icons.point_of_sale_rounded,
            description:
                'Fires a 24V solenoid pulse through the thermal printer RJ11 kick port when tender completes or cashier triggers No Sale.',
            children: [
              SwitchListTile.adaptive(
                contentPadding: EdgeInsets.zero,
                title: const Text('Auto-Kick on Cash Tender'),
                subtitle: const Text('Kicks the drawer open automatically when a cash sale is confirmed'),
                value: config.autoKickDrawerOnCash,
                activeColor: AppColors.brandPrimary,
                onChanged: (v) {
                  ref.read(hardwareConfigProvider.notifier).updateConfig(
                        config.copyWith(autoKickDrawerOnCash: v),
                      );
                },
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  OutlinedButton.icon(
                    onPressed: _testingKick ? null : _testKickDrawer,
                    icon: _testingKick
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.lock_open_rounded),
                    label: const Text('Test Kick Cash Drawer'),
                  ),
                ],
              ),
            ],
          ),

          const SizedBox(height: 24),

          // ==========================================
          // 3. RS232 / USB PHYSICAL SCALE
          // ==========================================
          _buildCard(
            title: 'RS232 / USB Scale Interfacing',
            icon: Icons.scale_rounded,
            description:
                'Continuous serial polling of weight for grocery, meat, and dampa/palengke stalls into the Tingi/Weight screen.',
            children: [
              SwitchListTile.adaptive(
                contentPadding: EdgeInsets.zero,
                title: const Text('Use Scale Simulator Mode'),
                subtitle: const Text('Enables interactive software scale for development and tablets without RS232 COM ports'),
                value: config.scaleSimulator,
                activeColor: AppColors.brandPrimary,
                onChanged: (v) {
                  ref.read(hardwareConfigProvider.notifier).updateConfig(
                        config.copyWith(scaleSimulator: v),
                      );
                },
              ),
              const SizedBox(height: 12),

              Row(
                children: [
                  const Expanded(
                    flex: 3,
                    child: Text(
                      'Scale Protocol',
                      style: TextStyle(fontWeight: FontWeight.w600),
                    ),
                  ),
                  Expanded(
                    flex: 5,
                    child: DropdownButtonFormField<ScaleProtocol>(
                      value: config.scaleProtocol,
                      decoration: const InputDecoration(border: OutlineInputBorder()),
                      items: const [
                        DropdownMenuItem(
                          value: ScaleProtocol.cas,
                          child: Text('CAS Protocol (AP-1 / ER Plus / SW-1 / PD-II)'),
                        ),
                        DropdownMenuItem(
                          value: ScaleProtocol.mettlerToledo,
                          child: Text('Mettler-Toledo Protocol (MT-SICS / Toledo 8217)'),
                        ),
                      ],
                      onChanged: (v) {
                        if (v != null) {
                          ref.read(hardwareConfigProvider.notifier).updateConfig(
                                config.copyWith(scaleProtocol: v),
                              );
                        }
                      },
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),

              if (!config.scaleSimulator) ...[
                Row(
                  children: [
                    Expanded(
                      flex: 3,
                      child: TextField(
                        controller: _scalePortController,
                        decoration: const InputDecoration(
                          labelText: 'Scale COM Port',
                          hintText: 'COM3 or /dev/ttyUSB1',
                          border: OutlineInputBorder(),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      flex: 2,
                      child: DropdownButtonFormField<int>(
                        value: config.scaleBaudRate,
                        decoration: const InputDecoration(
                          labelText: 'Baud Rate',
                          border: OutlineInputBorder(),
                        ),
                        items: const [
                          DropdownMenuItem(value: 4800, child: Text('4800')),
                          DropdownMenuItem(value: 9600, child: Text('9600 (Std)')),
                          DropdownMenuItem(value: 19200, child: Text('19200')),
                        ],
                        onChanged: (v) {
                          if (v != null) {
                            ref.read(hardwareConfigProvider.notifier).updateConfig(
                                  config.copyWith(scaleBaudRate: v),
                                );
                          }
                        },
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
              ],

              // Live Scale Monitor preview in settings
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: const Color(0xFF0F172A),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'LIVE SCALE MONITOR',
                          style: GoogleFonts.plusJakartaSans(
                            color: Colors.white60,
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Row(
                          children: [
                            Text(
                              scaleReading.weight.toStringAsFixed(3),
                              style: GoogleFonts.jetBrainsMono(
                                color: Colors.white,
                                fontSize: 28,
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                            const SizedBox(width: 6),
                            Text(
                              scaleReading.unit,
                              style: const TextStyle(color: Colors.white70, fontSize: 16),
                            ),
                          ],
                        ),
                      ],
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: scaleReading.isStable
                            ? AppColors.accentEmerald.withValues(alpha: 0.2)
                            : Colors.amber.withValues(alpha: 0.2),
                        borderRadius: BorderRadius.circular(AppRadius.full),
                      ),
                      child: Text(
                        scaleReading.isStable ? '● STABLE' : '◌ MOTION',
                        style: TextStyle(
                          color: scaleReading.isStable ? AppColors.accentEmerald : Colors.amber,
                          fontWeight: FontWeight.w700,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),

          const SizedBox(height: 24),

          // ==========================================
          // 4. CUSTOMER FACING DISPLAY (CFD)
          // ==========================================
          _buildCard(
            title: 'Customer Facing Display (CFD)',
            icon: Icons.tv_rounded,
            description:
                'Secondary HDMI monitor support or local HTTP/WebSocket server for tethered Android tablets showing live items and dynamic QR Ph codes.',
            children: [
              SwitchListTile.adaptive(
                contentPadding: EdgeInsets.zero,
                title: const Text('Enable Local CFD Web & WebSocket Server'),
                subtitle: const Text('Serves live customer display over LAN/tether for Android tablets'),
                value: config.cfdServerEnabled,
                activeColor: AppColors.brandPrimary,
                onChanged: (v) {
                  ref.read(hardwareConfigProvider.notifier).updateConfig(
                        config.copyWith(cfdServerEnabled: v),
                      );
                  if (v) {
                    cfdService.startServer();
                  } else {
                    cfdService.stopServer();
                  }
                },
              ),
              const SizedBox(height: 12),

              Row(
                children: [
                  Expanded(
                    flex: 3,
                    child: TextField(
                      controller: _cfdPortController,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Local CFD HTTP Port',
                        hintText: '8088',
                        border: OutlineInputBorder(),
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    flex: 4,
                    child: Text(
                      cfdService.isServerRunning
                          ? '● Server Running (${cfdService.connectedClientsCount} Clients)'
                          : '○ Server Stopped',
                      style: TextStyle(
                        color: cfdService.isServerRunning ? AppColors.accentEmerald : Colors.grey,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),

              // Tethering instructions, second-device URL, and QR code
              Builder(builder: (context) {
                final lanUrl = _lanAddress != null
                    ? 'http://${_lanAddress!}:${config.cfdPort}/cfd'
                    : null;
                return Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: AppColors.cardHover,
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Customer Display URL (open on the second device):',
                        style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13),
                      ),
                      const SizedBox(height: 4),
                      SelectableText(
                        lanUrl ?? 'http://localhost:${config.cfdPort}/cfd',
                        style: GoogleFonts.jetBrainsMono(
                          fontSize: 14,
                          color: AppColors.brandPrimary,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        lanUrl != null
                            ? 'Open a browser on a second tablet, monitor, or phone connected to the same Wi-Fi/LAN and go to this address, or scan the QR code below.'
                            : 'Could not detect this device\'s LAN IP address. Connect to Wi-Fi/Ethernet and reopen this screen, or find the IP manually in your device\'s network settings.',
                        style: const TextStyle(fontSize: 12, color: AppColors.textSecondary),
                      ),
                      if (lanUrl != null) ...[
                        const SizedBox(height: 16),
                        Center(
                          child: Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: Colors.white,
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: Image.network(
                              'https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${Uri.encodeComponent(lanUrl)}',
                              width: 160,
                              height: 160,
                              errorBuilder: (_, __, ___) => const SizedBox(
                                width: 160,
                                height: 160,
                                child: Center(child: Text('QR unavailable', style: TextStyle(color: Colors.black))),
                              ),
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                );
              }),

              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: () {
                  Navigator.of(context).push<void>(
                    MaterialPageRoute(
                      builder: (_) => const CustomerFacingDisplayScreen(),
                    ),
                  );
                },
                icon: const Icon(Icons.open_in_new_rounded),
                label: const Text('Open Native CFD Screen (Secondary Monitor)'),
              ),
            ],
          ),
          const SizedBox(height: 40),
        ],
      ),
    );
  }

  Widget _buildCard({
    required String title,
    required IconData icon,
    required String description,
    required List<Widget> children,
  }) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.card,
      ),
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: AppColors.brandPrimaryContainer,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(icon, color: AppColors.brandPrimary, size: 22),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: GoogleFonts.outfit(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    Text(
                      description,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          const Divider(color: AppColors.border, height: 1),
          const SizedBox(height: 20),
          ...children,
        ],
      ),
    );
  }
}
