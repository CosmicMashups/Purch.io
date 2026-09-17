import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/hardware/scale/scale_parser.dart';

void main() {
  group('ScaleParser', () {
    group('CAS Protocol', () {
      test('parses stable gross reading correctly', () {
        const raw = 'ST,GS,+  1.250kg\r\n';
        final reading = ScaleParser.parseCas(raw);

        expect(reading, isNotNull);
        expect(reading!.weight, closeTo(1.250, 0.0001));
        expect(reading.unit, equals('kg'));
        expect(reading.isStable, isTrue);
        expect(reading.isOverload, isFalse);
        expect(reading.isTare, isFalse);
      });

      test('parses unstable / in-motion reading correctly', () {
        const raw = 'US,GS,+  0.850kg\r\n';
        final reading = ScaleParser.parseCas(raw);

        expect(reading, isNotNull);
        expect(reading!.weight, closeTo(0.850, 0.0001));
        expect(reading.isStable, isFalse);
        expect(reading.isOverload, isFalse);
      });

      test('parses net weight (with tare) correctly', () {
        const raw = 'ST,NT,+  0.400kg\r\n';
        final reading = ScaleParser.parseCas(raw);

        expect(reading, isNotNull);
        expect(reading!.weight, closeTo(0.400, 0.0001));
        expect(reading.isTare, isTrue);
        expect(reading.isStable, isTrue);
      });

      test('detects overload condition correctly', () {
        const raw = 'OL,GS,+  99.999kg\r\n';
        final reading = ScaleParser.parseCas(raw);

        expect(reading, isNotNull);
        expect(reading!.isOverload, isTrue);
        expect(reading.isStable, isFalse);
      });
    });

    group('Mettler-Toledo MT-SICS Protocol', () {
      test('parses stable MT-SICS S S response', () {
        const raw = 'S S      2.450 kg\r\n';
        final reading = ScaleParser.parseMettlerToledo(raw);

        expect(reading, isNotNull);
        expect(reading!.weight, closeTo(2.450, 0.0001));
        expect(reading.unit, equals('kg'));
        expect(reading.isStable, isTrue);
        expect(reading.isOverload, isFalse);
      });

      test('parses dynamic/unstable MT-SICS S D response', () {
        const raw = 'S D      1.100 kg\r\n';
        final reading = ScaleParser.parseMettlerToledo(raw);

        expect(reading, isNotNull);
        expect(reading!.weight, closeTo(1.100, 0.0001));
        expect(reading.isStable, isFalse);
        expect(reading.isOverload, isFalse);
      });

      test('detects overload response S +', () {
        const raw = 'S +\r\n';
        final reading = ScaleParser.parseMettlerToledo(raw);

        expect(reading, isNotNull);
        expect(reading!.isOverload, isTrue);
        expect(reading.isStable, isFalse);
      });
    });
  });
}
