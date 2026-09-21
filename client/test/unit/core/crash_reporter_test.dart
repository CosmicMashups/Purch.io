import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/diagnostics/crash_reporter.dart';

class _Recording implements CrashReporter {
  final sources = <String>[];

  @override
  void report(Object error, StackTrace? stackTrace, {required String source}) {
    sources.add(source);
  }
}

void main() {
  late Directory dir;
  late File file;

  setUp(() {
    dir = Directory.systemTemp.createTempSync('crash_reporter_test');
    file = File('${dir.path}/crash.log');
  });

  tearDown(() => dir.deleteSync(recursive: true));

  test('writes the source, error and stack trace', () {
    FileCrashReporter(file).report(
      StateError('boom'),
      StackTrace.fromString('#0 main (file.dart:1)'),
      source: 'flutter',
    );

    final text = file.readAsStringSync();
    expect(text, contains('[flutter] Bad state: boom'));
    expect(text, contains('#0 main (file.dart:1)'));
  });

  test('a crash loop is written once, then again after the window', () {
    var now = DateTime.utc(2026, 9, 21, 10);
    final reporter = FileCrashReporter(file, clock: () => now);
    final stack = StackTrace.fromString('#0 loop (file.dart:9)');

    reporter.report(StateError('again'), stack, source: 'flutter');
    now = now.add(const Duration(seconds: 1));
    reporter.report(StateError('again'), stack, source: 'flutter');
    expect('Bad state: again'.allMatches(file.readAsStringSync()), hasLength(1));

    now = now.add(const Duration(seconds: 30));
    reporter.report(StateError('again'), stack, source: 'flutter');
    expect('Bad state: again'.allMatches(file.readAsStringSync()), hasLength(2));
  });

  test('a different error is never treated as a duplicate', () {
    FileCrashReporter(file)
      ..report(StateError('one'), null, source: 'flutter')
      ..report(StateError('two'), null, source: 'flutter');

    final text = file.readAsStringSync();
    expect(text, contains('Bad state: one'));
    expect(text, contains('Bad state: two'));
  });

  test('the file rolls to .1 past the size cap, so it cannot grow without bound', () {
    final reporter = FileCrashReporter(
      file,
      maxBytes: 200,
      duplicateWindow: Duration.zero,
    );

    for (var i = 0; i < 20; i++) {
      reporter.report(
        StateError('error number $i ${'x' * 40}'),
        null,
        source: 'flutter',
      );
    }

    expect(File('${file.path}.1').existsSync(), isTrue);
    expect(file.lengthSync(), lessThan(200 + 300)); // at most one entry past the cap
  });

  test('a location that cannot be written to never throws', () {
    file.createSync(); // a file where the reporter expects a directory
    final blocked = File('${file.path}/inside.log');

    expect(
      () => FileCrashReporter(blocked).report(StateError('x'), null, source: 'flutter'),
      returnsNormally,
    );
  });

  test('installed handlers forward framework and platform errors', () {
    final recording = _Recording();
    final previousFlutter = FlutterError.onError;
    final previousPlatform = PlatformDispatcher.instance.onError;
    final originalDebugPrint = debugPrint;
    addTearDown(() {
      FlutterError.onError = previousFlutter;
      PlatformDispatcher.instance.onError = previousPlatform;
      debugPrint = originalDebugPrint;
    });
    // presentError and the fallback log print to the console; keep the test output clean.
    debugPrint = (message, {wrapWidth}) {};

    installGlobalErrorHandlers(recording);
    FlutterError.onError!(FlutterErrorDetails(exception: StateError('widget broke')));
    final handled = PlatformDispatcher.instance.onError!(
      StateError('async broke'),
      StackTrace.empty,
    );

    expect(handled, isTrue);
    expect(recording.sources, ['flutter', 'platform']);
  });
}
