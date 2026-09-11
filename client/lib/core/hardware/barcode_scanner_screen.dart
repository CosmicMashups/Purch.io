import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

/// Camera-based barcode scanning, shared across any screen that needs to
/// scan-to-fill or scan-to-look-up a barcode (e.g. AddItemScreen). Pops with
/// the scanned code as a String once one is found — the caller never has to
/// know anything about the camera or the scanning package underneath.
///
/// USB/Bluetooth "keyboard wedge" scanners need no such screen — they emit
/// fast keystrokes into whatever text field already has focus, so a plain
/// TextFormField already supports them without any extra code.
class BarcodeScannerScreen extends StatefulWidget {
  const BarcodeScannerScreen({super.key});

  @override
  State<BarcodeScannerScreen> createState() => _BarcodeScannerScreenState();
}

class _BarcodeScannerScreenState extends State<BarcodeScannerScreen> {
  final MobileScannerController _controller = MobileScannerController(
    detectionSpeed: DetectionSpeed.noDuplicates,
  );
  bool _handled = false;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_handled) {
      return;
    }
    if (capture.barcodes.isEmpty) {
      return;
    }
    final value = capture.barcodes.first.rawValue;
    if (value == null || value.isEmpty) {
      return;
    }
    _handled = true;
    Navigator.of(context).pop(value);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Scan Barcode'),
        actions: [
          IconButton(
            onPressed: () => _controller.toggleTorch(),
            icon: const Icon(Icons.flash_on),
            tooltip: 'Toggle flash',
          ),
        ],
      ),
      body: MobileScanner(
        controller: _controller,
        onDetect: _onDetect,
        errorBuilder: (context, error, child) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Text(
                'Could not access the camera: ${error.errorDetails?.message ?? error.errorCode}',
                textAlign: TextAlign.center,
              ),
            ),
          );
        },
      ),
    );
  }
}
