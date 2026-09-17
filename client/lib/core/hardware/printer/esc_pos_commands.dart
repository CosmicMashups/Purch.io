/// Low-level ESC/POS byte sequences and command helpers for thermal printers
/// (Epson, Star Micronics, Xprinter, Sunmi, Rongta, etc.).
///
/// Designed to run in pure Dart across Windows, Linux, Android, and macOS
/// without relying on external native binaries.
library;

class EscPosCommands {
  const EscPosCommands._();

  // --- ASCII & Control Codes ---
  static const int nul = 0x00;
  static const int eot = 0x04;
  static const int enq = 0x05;
  static const int bel = 0x07;
  static const int ht = 0x09; // Horizontal tab
  static const int lf = 0x0A; // Line feed
  static const int ff = 0x0C; // Form feed (Pole display clear)
  static const int cr = 0x0D; // Carriage return
  static const int dle = 0x10;
  static const int can = 0x18;
  static const int esc = 0x1B;
  static const int fs = 0x1C;
  static const int gs = 0x1D;

  // --- Initialization ---
  /// ESC @ — Initialize printer (resets formatting, buffers, tabs).
  static const List<int> init = [esc, 0x40];

  // --- Text Alignment ---
  /// ESC a n — Select justification: 0: Left, 1: Center, 2: Right.
  static List<int> alignLeft() => [esc, 0x61, 0x00];
  static List<int> alignCenter() => [esc, 0x61, 0x01];
  static List<int> alignRight() => [esc, 0x61, 0x02];

  // --- Font Styling ---
  /// ESC E n — Turn emphasized (bold) mode on (1) or off (0).
  static List<int> bold(bool enabled) => [esc, 0x45, enabled ? 0x01 : 0x00];

  /// ESC - n — Turn underline mode on (1 or 2) or off (0).
  static List<int> underline(bool enabled, {bool doubleThickness = false}) =>
      [esc, 0x2D, enabled ? (doubleThickness ? 0x02 : 0x01) : 0x00];

  /// GS B n — Turn white/black reverse printing mode on (1) or off (0).
  static List<int> reverse(bool enabled) => [gs, 0x42, enabled ? 0x01 : 0x00];

  /// ESC M n — Select character font: 0: Font A (12x24), 1: Font B (9x17).
  static List<int> selectFont({bool fontB = false}) =>
      [esc, 0x4D, fontB ? 0x01 : 0x00];

  /// GS ! n — Select character size (magnification width 0..7, height 0..7).
  static List<int> textSize({int widthMultiplier = 1, int heightMultiplier = 1}) {
    final w = (widthMultiplier.clamp(1, 8) - 1) << 4;
    final h = heightMultiplier.clamp(1, 8) - 1;
    return [gs, 0x21, w | h];
  }

  // --- Line Spacing & Feeds ---
  /// ESC 2 — Select default line spacing (1/6 inch = ~30 dots).
  static const List<int> defaultLineSpacing = [esc, 0x32];

  /// ESC 3 n — Set line spacing to n dots (1..255).
  static List<int> customLineSpacing(int n) => [esc, 0x33, n.clamp(1, 255)];

  /// ESC d n — Print and feed n lines.
  static List<int> feedLines(int n) => [esc, 0x64, n.clamp(1, 255)];

  // --- Paper Cutters ---
  /// GS V 0 — Full cut paper.
  static const List<int> fullCut = [gs, 0x56, 0x00];

  /// GS V 1 — Partial cut paper (leaves one small uncut bridge).
  static const List<int> partialCut = [gs, 0x56, 0x01];

  /// GS V 66 n — Feed paper n lines then partial cut.
  static List<int> feedAndCut({int feedLines = 3}) =>
      [gs, 0x56, 0x42, feedLines.clamp(0, 255)];

  // --- Cash Drawer RJ11 Kick Pulse ---
  /// ESC p m t1 t2 — Generate pulse to cash drawer solenoid.
  /// m: 0 = Pin 2 (standard 24V solenoid), 1 = Pin 5.
  /// t1: Pulse ON time = t1 * 2ms. 25 * 2ms = 50ms.
  /// t2: Pulse OFF time = t2 * 2ms. 250 * 2ms = 500ms.
  static List<int> drawerKickPin2({int onTimeMs = 50, int offTimeMs = 500}) {
    final t1 = (onTimeMs ~/ 2).clamp(1, 255);
    final t2 = (offTimeMs ~/ 2).clamp(1, 255);
    return [esc, 0x70, 0x00, t1, t2];
  }

  static List<int> drawerKickPin5({int onTimeMs = 50, int offTimeMs = 500}) {
    final t1 = (onTimeMs ~/ 2).clamp(1, 255);
    final t2 = (offTimeMs ~/ 2).clamp(1, 255);
    return [esc, 0x70, 0x01, t1, t2];
  }

  /// Star Micronics drawer kick command: BEL (0x07).
  static const List<int> starDrawerKick = [bel];

  // --- 2D QR Code Generation (ESC/POS native) ---
  /// Emits standard ESC/POS GS ( k commands to print a 2D QR Code (Model 2).
  ///
  /// [content] is the encoded payload (e.g. dynamic QR Ph string or URL).
  /// [moduleSize] is dot size per module (1..16, typically 4..8 for 58/80mm).
  /// [errorCorrection] 48: Level L (7%), 49: Level M (15%), 50: Level Q (25%), 51: Level H (30%).
  static List<int> qrCode(
    String content, {
    int moduleSize = 5,
    int errorCorrection = 49,
  }) {
    final bytes = <int>[];
    final rawContent = content.codeUnits;
    final length = rawContent.length + 3;
    final pL = length & 0xFF;
    final pH = (length >> 8) & 0xFF;

    // 1. Set model (Model 2 is standard)
    bytes.addAll([gs, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00]);

    // 2. Set module size
    bytes.addAll([gs, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, moduleSize.clamp(1, 16)]);

    // 3. Set error correction level
    bytes.addAll([gs, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, errorCorrection]);

    // 4. Store symbol data
    bytes.addAll([gs, 0x28, 0x6B, pL, pH, 0x31, 0x50, 0x30]);
    bytes.addAll(rawContent);

    // 5. Print the symbol
    bytes.addAll([gs, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30]);

    return bytes;
  }

  // --- 1D Barcode Generation (Code128) ---
  /// Emits standard GS k Code128 barcode commands.
  static List<int> barcode128(String content, {int heightDots = 60, bool showText = true}) {
    final bytes = <int>[];
    // Set barcode height
    bytes.addAll([gs, 0x68, heightDots.clamp(1, 255)]);
    // Set HRI characters position (0: none, 2: below)
    bytes.addAll([gs, 0x48, showText ? 0x02 : 0x00]);
    // GS k 73 (Code128 type B)
    final codeUnits = content.codeUnits;
    bytes.addAll([gs, 0x6B, 0x49, codeUnits.length]);
    bytes.addAll(codeUnits);
    return bytes;
  }

  // --- Raster Bit Image (GS v 0) ---
  /// GS v 0 m xL xH yL yH d1...dk
  /// Prints a 1-bit monochrome raster bitmap image.
  /// [widthBytes] is image pixel width / 8.
  /// [heightPixels] is image pixel height.
  /// [pixelBytes] is binary 1-bit packed byte array (1 = black, 0 = white).
  static List<int> rasterImage({
    required int widthBytes,
    required int heightPixels,
    required List<int> pixelBytes,
  }) {
    final bytes = <int>[];
    final xL = widthBytes & 0xFF;
    final xH = (widthBytes >> 8) & 0xFF;
    final yL = heightPixels & 0xFF;
    final yH = (heightPixels >> 8) & 0xFF;

    bytes.addAll([gs, 0x76, 0x30, 0x00, xL, xH, yL, yH]);
    bytes.addAll(pixelBytes);
    return bytes;
  }
}
