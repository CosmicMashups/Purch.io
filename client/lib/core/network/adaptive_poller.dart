import 'dart:async';
import 'dart:math';

import 'package:flutter/widgets.dart';

/// Repeats [onTick] for an unattended display without hammering the API.
///
/// - Polls every [active] (±[jitterRatio]) while [isBusy] says there is something to watch, then every [idle]
///   once it has been quiet for [quietAfter]. Any busy answer snaps it back to [active].
/// - Stops while the app is paused/inactive/hidden and ticks once straight away on resume, so a backgrounded
///   terminal does not keep polling and a foregrounded one is current immediately.
/// - Jitter keeps terminals that restarted together from polling in lockstep.
class AdaptivePoller with WidgetsBindingObserver {
  AdaptivePoller({
    required this.onTick,
    required this.isBusy,
    this.active = const Duration(seconds: 5),
    this.idle = const Duration(seconds: 15),
    this.quietAfter = const Duration(minutes: 5),
    this.jitterRatio = 0.15,
    Random? random,
    DateTime Function()? clock,
  })  : _random = random ?? Random(),
        _clock = clock ?? DateTime.now;

  final void Function() onTick;
  final bool Function() isBusy;
  final Duration active;
  final Duration idle;
  final Duration quietAfter;
  final double jitterRatio;
  final Random _random;
  final DateTime Function() _clock;

  Timer? _timer;
  bool _running = false;
  DateTime _lastBusyAt = DateTime.fromMillisecondsSinceEpoch(0);

  /// The delay before the next tick; public so it can be unit-tested without timers.
  Duration nextDelay() {
    final now = _clock();
    if (isBusy()) _lastBusyAt = now;
    final quietFor = now.difference(_lastBusyAt);
    final base = quietFor >= quietAfter ? idle : active;
    final factor = 1 + (_random.nextDouble() * 2 - 1) * jitterRatio;
    return Duration(milliseconds: max(1000, (base.inMilliseconds * factor).round()));
  }

  void start() {
    if (_running) return;
    _running = true;
    _lastBusyAt = _clock();
    WidgetsBinding.instance.addObserver(this);
    _schedule();
  }

  void stop() {
    _running = false;
    _timer?.cancel();
    _timer = null;
    WidgetsBinding.instance.removeObserver(this);
  }

  void _schedule() {
    _timer?.cancel();
    if (!_running) return;
    _timer = Timer(nextDelay(), () {
      onTick();
      _schedule();
    });
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (!_running) return;
    if (state == AppLifecycleState.resumed) {
      onTick();
      _schedule();
    } else {
      _timer?.cancel();
      _timer = null;
    }
  }
}
