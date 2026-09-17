import 'dart:convert';
import 'esc_pos_commands.dart';

/// Paper width specification for thermal receipt printers.
enum PaperWidth {
  mm58(charsPerLineFontA: 32, charsPerLineFontB: 42, dotsPerLine: 384),
  mm80(charsPerLineFontA: 48, charsPerLineFontB: 64, dotsPerLine: 576);

  const PaperWidth({
    required this.charsPerLineFontA,
    required this.charsPerLineFontB,
    required this.dotsPerLine,
  });

  final int charsPerLineFontA;
  final int charsPerLineFontB;
  final int dotsPerLine;
}

enum PrintAlignment { left, center, right }

class TableColumn {
  const TableColumn({
    required this.text,
    required this.widthRatio,
    this.alignment = PrintAlignment.left,
  });

  final String text;
  final double widthRatio;
  final PrintAlignment alignment;
}

/// Fluent builder for composing ESC/POS byte-streams.
class EscPosBuilder {
  EscPosBuilder({this.paperWidth = PaperWidth.mm80, bool initialize = true}) {
    if (initialize) {
      _bytes.addAll(EscPosCommands.init);
    }
  }

  final PaperWidth paperWidth;
  final List<int> _bytes = [];

  bool _isFontB = false;

  int get charsPerLine =>
      _isFontB ? paperWidth.charsPerLineFontB : paperWidth.charsPerLineFontA;

  List<int> toBytes() => List.unmodifiable(_bytes);

  EscPosBuilder raw(List<int> bytes) {
    _bytes.addAll(bytes);
    return this;
  }

  EscPosBuilder align(PrintAlignment alignment) {
    switch (alignment) {
      case PrintAlignment.left:
        _bytes.addAll(EscPosCommands.alignLeft());
        break;
      case PrintAlignment.center:
        _bytes.addAll(EscPosCommands.alignCenter());
        break;
      case PrintAlignment.right:
        _bytes.addAll(EscPosCommands.alignRight());
        break;
    }
    return this;
  }

  EscPosBuilder font({bool fontB = false}) {
    _isFontB = fontB;
    _bytes.addAll(EscPosCommands.selectFont(fontB: fontB));
    return this;
  }

  EscPosBuilder bold(bool enabled) {
    _bytes.addAll(EscPosCommands.bold(enabled));
    return this;
  }

  EscPosBuilder underline(bool enabled, {bool doubleThickness = false}) {
    _bytes.addAll(
      EscPosCommands.underline(enabled, doubleThickness: doubleThickness),
    );
    return this;
  }

  EscPosBuilder reverse(bool enabled) {
    _bytes.addAll(EscPosCommands.reverse(enabled));
    return this;
  }

  EscPosBuilder size({int width = 1, int height = 1}) {
    _bytes.addAll(
      EscPosCommands.textSize(widthMultiplier: width, heightMultiplier: height),
    );
    return this;
  }

  EscPosBuilder lineFeed([int count = 1]) {
    for (var i = 0; i < count; i++) {
      _bytes.add(EscPosCommands.lf);
    }
    return this;
  }

  EscPosBuilder feedLines(int count) {
    _bytes.addAll(EscPosCommands.feedLines(count));
    return this;
  }

  /// Sanitizes text for thermal printers:
  /// Normalizes Philippine Peso symbol ₱ -> "PHP " or "P" to avoid corrupting CP437.
  String _sanitizeText(String text) {
    return text.replaceAll('₱', 'PHP ');
  }

  /// Prints a single line of text with trailing line feed.
  EscPosBuilder text(
    String line, {
    PrintAlignment? alignment,
    bool? isBold,
    int? widthMultiplier,
    int? heightMultiplier,
  }) {
    if (alignment != null) align(alignment);
    if (isBold != null) bold(isBold);
    if (widthMultiplier != null || heightMultiplier != null) {
      size(
        width: widthMultiplier ?? 1,
        height: heightMultiplier ?? 1,
      );
    }

    final sanitized = _sanitizeText(line);
    _bytes.addAll(utf8.encode(sanitized));
    _bytes.add(EscPosCommands.lf);

    // Reset temporary styles
    if (isBold != null) bold(false);
    if (widthMultiplier != null || heightMultiplier != null) size(width: 1, height: 1);
    return this;
  }

  /// Prints a divider line spanning the full paper width.
  EscPosBuilder divider([String char = '-']) {
    final line = char * charsPerLine;
    return text(line, alignment: PrintAlignment.left);
  }

  /// Prints a two-column row, with [left] aligned left and [right] aligned right.
  /// E.g. "Total                        PHP 540.00"
  EscPosBuilder twoColumn(
    String left,
    String right, {
    bool isBold = false,
    String padChar = ' ',
  }) {
    if (isBold) bold(true);

    final sanitizedLeft = _sanitizeText(left);
    final sanitizedRight = _sanitizeText(right);
    final totalCols = charsPerLine;

    final availableSpace = totalCols - sanitizedRight.length;
    if (availableSpace <= 0) {
      text(sanitizedLeft);
      text(sanitizedRight, alignment: PrintAlignment.right);
    } else {
      var trimmedLeft = sanitizedLeft;
      if (trimmedLeft.length > availableSpace - 1) {
        trimmedLeft = '${trimmedLeft.substring(0, availableSpace - 2)}.';
      }
      final paddingNeeded = totalCols - trimmedLeft.length - sanitizedRight.length;
      final padding = padChar * (paddingNeeded > 0 ? paddingNeeded : 1);
      _bytes.addAll(utf8.encode('$trimmedLeft$padding$sanitizedRight'));
      _bytes.add(EscPosCommands.lf);
    }

    if (isBold) bold(false);
    return this;
  }

  /// Prints formatted columns in a single row based on width ratios.
  EscPosBuilder tableRow(List<TableColumn> columns) {
    final totalCols = charsPerLine;
    final rowBuffer = StringBuffer();

    // Calculate column character widths
    final colWidths = <int>[];
    var allocated = 0;
    for (var i = 0; i < columns.length; i++) {
      if (i == columns.length - 1) {
        colWidths.add((totalCols - allocated).clamp(1, totalCols));
      } else {
        final w = (columns[i].widthRatio * totalCols).round().clamp(1, totalCols);
        colWidths.add(w);
        allocated += w;
      }
    }

    for (var i = 0; i < columns.length; i++) {
      final col = columns[i];
      final width = colWidths[i];
      final text = _sanitizeText(col.text);

      String cellText;
      if (text.length > width) {
        cellText = width > 1 ? '${text.substring(0, width - 1)}.' : text.substring(0, width);
      } else {
        cellText = text;
      }

      final padCount = width - cellText.length;
      if (padCount <= 0) {
        rowBuffer.write(cellText);
      } else {
        switch (col.alignment) {
          case PrintAlignment.left:
            rowBuffer.write(cellText);
            rowBuffer.write(' ' * padCount);
            break;
          case PrintAlignment.right:
            rowBuffer.write(' ' * padCount);
            rowBuffer.write(cellText);
            break;
          case PrintAlignment.center:
            final leftPad = padCount ~/ 2;
            final rightPad = padCount - leftPad;
            rowBuffer.write(' ' * leftPad);
            rowBuffer.write(cellText);
            rowBuffer.write(' ' * rightPad);
            break;
        }
      }
    }

    _bytes.addAll(utf8.encode(rowBuffer.toString()));
    _bytes.add(EscPosCommands.lf);
    return this;
  }

  /// Native QR Code
  EscPosBuilder qrCode(String data, {int moduleSize = 5}) {
    align(PrintAlignment.center);
    _bytes.addAll(EscPosCommands.qrCode(data, moduleSize: moduleSize));
    _bytes.add(EscPosCommands.lf);
    return this;
  }

  /// 1D Barcode
  EscPosBuilder barcode(String data, {int height = 50, bool showText = true}) {
    align(PrintAlignment.center);
    _bytes.addAll(EscPosCommands.barcode128(data, heightDots: height, showText: showText));
    _bytes.add(EscPosCommands.lf);
    return this;
  }

  /// Cash drawer kick pulse
  EscPosBuilder kickDrawer({bool pin5 = false}) {
    _bytes.addAll(
      pin5 ? EscPosCommands.drawerKickPin5() : EscPosCommands.drawerKickPin2(),
    );
    return this;
  }

  /// Feed and cut paper
  EscPosBuilder cut({int feedLines = 3, bool partial = true}) {
    if (partial) {
      _bytes.addAll(EscPosCommands.feedAndCut(feedLines: feedLines));
    } else {
      if (feedLines > 0) this.feedLines(feedLines);
      _bytes.addAll(EscPosCommands.fullCut);
    }
    return this;
  }
}
