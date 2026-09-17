import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/hardware/printer/esc_pos_builder.dart';
import 'package:purch_client/core/hardware/printer/esc_pos_commands.dart';

void main() {
  group('EscPosBuilder', () {
    test('initializes with ESC @ and default 80mm width', () {
      final builder = EscPosBuilder();
      final bytes = builder.toBytes();

      expect(bytes.sublist(0, 2), equals(EscPosCommands.init));
      expect(builder.paperWidth, equals(PaperWidth.mm80));
      expect(builder.charsPerLine, equals(48));
    });

    test('emits alignment commands correctly', () {
      final builder = EscPosBuilder();
      builder.align(PrintAlignment.center);
      expect(builder.toBytes(), containsAllInOrder(EscPosCommands.alignCenter()));

      builder.align(PrintAlignment.right);
      expect(builder.toBytes(), containsAllInOrder(EscPosCommands.alignRight()));

      builder.align(PrintAlignment.left);
      expect(builder.toBytes(), containsAllInOrder(EscPosCommands.alignLeft()));
    });

    test('formats twoColumn row with proper width padding', () {
      final builder = EscPosBuilder(paperWidth: PaperWidth.mm80);
      builder.twoColumn('Total', 'PHP 100.00');
      final text = utf8.decode(builder.toBytes().sublist(2)); // Skip init

      expect(text, startsWith('Total'));
      expect(text.trim(), endsWith('PHP 100.00'));
      expect(text.trim().length, equals(48)); // 80mm character width
    });

    test('formats twoColumn row for 58mm width properly', () {
      final builder = EscPosBuilder(paperWidth: PaperWidth.mm58);
      builder.twoColumn('Subtotal', 'PHP 50.00');
      final text = utf8.decode(builder.toBytes().sublist(2));

      expect(text, startsWith('Subtotal'));
      expect(text.trim(), endsWith('PHP 50.00'));
      expect(text.trim().length, equals(32)); // 58mm character width
    });

    test('replaces Philippine Peso ₱ with PHP to avoid CP437 corruption', () {
      final builder = EscPosBuilder();
      builder.text('₱120.00');
      final text = utf8.decode(builder.toBytes().sublist(2));

      expect(text, contains('PHP 120.00'));
      expect(text, isNot(contains('₱')));
    });

    test('emits full cut and partial cut commands', () {
      final builder = EscPosBuilder(initialize: false);
      builder.cut(feedLines: 3, partial: true);
      expect(builder.toBytes(), equals(EscPosCommands.feedAndCut(feedLines: 3)));

      final builder2 = EscPosBuilder(initialize: false);
      builder2.cut(feedLines: 0, partial: false);
      expect(builder2.toBytes(), equals(EscPosCommands.fullCut));
    });

    test('emits cash drawer kick pulse command for Pin 2 and Pin 5', () {
      final builder = EscPosBuilder(initialize: false);
      builder.kickDrawer(pin5: false);
      expect(builder.toBytes(), equals(EscPosCommands.drawerKickPin2()));

      final builder2 = EscPosBuilder(initialize: false);
      builder2.kickDrawer(pin5: true);
      expect(builder2.toBytes(), equals(EscPosCommands.drawerKickPin5()));
    });

    test('emits native QR code byte sequence', () {
      final builder = EscPosBuilder(initialize: false);
      builder.qrCode('https://purch.io');
      final bytes = builder.toBytes();

      // Check for GS ( k sequence
      expect(bytes, contains(EscPosCommands.gs));
      expect(bytes, contains(0x28));
      expect(bytes, contains(0x6B));
    });
  });
}
