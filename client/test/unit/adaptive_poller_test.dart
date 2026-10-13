import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/network/adaptive_poller.dart';

/// Always returns 0.5, so the jitter factor is exactly 1.
class _NoJitter implements Random {
  @override
  bool nextBool() => false;
  @override
  double nextDouble() => 0.5;
  @override
  int nextInt(int max) => 0;
}

void main() {
  test('polls fast while busy and backs off once quiet for the quiet period, then snaps back', () {
    var now = DateTime(2026, 1, 1, 9);
    var busy = true;
    final poller = AdaptivePoller(
      onTick: () {},
      isBusy: () => busy,
      random: _NoJitter(),
      clock: () => now,
    );

    expect(poller.nextDelay(), const Duration(seconds: 5));

    busy = false;
    now = now.add(const Duration(minutes: 4));
    expect(poller.nextDelay(), const Duration(seconds: 5));

    now = now.add(const Duration(minutes: 2));
    expect(poller.nextDelay(), const Duration(seconds: 15));

    busy = true;
    expect(poller.nextDelay(), const Duration(seconds: 5));
  });

  test('jitter stays within the ratio', () {
    final poller = AdaptivePoller(onTick: () {}, isBusy: () => true, random: Random(7));
    for (var i = 0; i < 200; i++) {
      final ms = poller.nextDelay().inMilliseconds;
      expect(ms, inInclusiveRange(4250, 5750));
    }
  });
}
