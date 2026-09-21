import 'dart:io';

import 'package:flutter/foundation.dart';

/// Where uncaught errors go. Today that is a local log file (see [FileCrashReporter]); a remote service
/// (Sentry, Crashlytics) can be added later by implementing this once, without touching the call sites.
abstract interface class CrashReporter {
  /// Must never throw: it runs inside error handlers, and an error while reporting an error hides both.
  void report(Object error, StackTrace? stackTrace, {required String source});
}

/// Appends each uncaught error to a plain-text file that support can ask a store owner to send. Bounded:
/// when the file passes [maxBytes] it is rolled to `<name>.1` (replacing the previous roll), so a crash loop
/// can't fill a terminal's storage. An identical error repeating within [duplicateWindow] is written once,
/// for the same reason.
class FileCrashReporter implements CrashReporter {
  FileCrashReporter(
    this._file, {
    this.maxBytes = 256 * 1024,
    this.duplicateWindow = const Duration(seconds: 10),
    DateTime Function()? clock,
  }) : _clock = clock ?? DateTime.now;

  final File _file;
  final int maxBytes;
  final Duration duplicateWindow;
  final DateTime Function() _clock;

  String? _lastSignature;
  DateTime? _lastAt;

  @override
  void report(Object error, StackTrace? stackTrace, {required String source}) {
    try {
      final now = _clock();
      final firstFrame = stackTrace?.toString().split('\n').first ?? '';
      final signature = '$source|${error.runtimeType}|$error|$firstFrame';
      final repeated =
          signature == _lastSignature &&
          _lastAt != null &&
          now.difference(_lastAt!) < duplicateWindow;
      _lastSignature = signature;
      _lastAt = now;
      if (repeated) return;

      _rollIfLarge();
      _file.parent.createSync(recursive: true);
      _file.writeAsStringSync(
        '${now.toUtc().toIso8601String()} [$source] $error\n'
        '${stackTrace ?? '(no stack trace)'}\n'
        '---\n',
        mode: FileMode.append,
        flush: true,
      );
    } on Object catch (problem) {
      // Reporting is best-effort; the console is the last resort.
      debugPrint('CrashReporter could not write: $problem');
    }
  }

  void _rollIfLarge() {
    if (_file.existsSync() && _file.lengthSync() > maxBytes) {
      _file.renameSync('${_file.path}.1');
    }
  }
}

/// Routes every uncaught error to [reporter]: Flutter framework errors (build/layout/gesture) and errors
/// that escape async code, which reach the platform dispatcher. Framework errors are still shown as before
/// (red screen in debug, console in release).
void installGlobalErrorHandlers(CrashReporter reporter) {
  FlutterError.onError = (details) {
    reporter.report(details.exception, details.stack, source: 'flutter');
    FlutterError.presentError(details);
  };

  PlatformDispatcher.instance.onError = (error, stack) {
    reporter.report(error, stack, source: 'platform');
    debugPrint('Uncaught error: $error\n$stack');
    // true = handled, so the engine does not also terminate the isolate.
    return true;
  };
}
