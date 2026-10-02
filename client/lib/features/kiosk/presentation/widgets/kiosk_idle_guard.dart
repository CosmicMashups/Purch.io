import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../providers/kiosk_providers.dart';

/// How long a cart with items sits untouched before the kiosk assumes the
/// customer walked away and resets for the next one (E6 design decision).
const kioskIdleResetDuration = Duration(seconds: 90);

/// Wraps the whole app (via [MaterialApp.router]'s `builder`, above its own
/// Navigator) so it sees every tap no matter how deep the kiosk has pushed —
/// menu, cart, fulfillment all share the same root Navigator kiosk screens
/// push onto, so a guard scoped to just the landing screen would be covered
/// by them and never see a tap at all. Being above the Navigator also means
/// this widget's own BuildContext cannot reach [GoRouter.of] or
/// [Navigator.of] — [router] is taken directly instead of looked up.
///
/// Idle only matters while the device is actually inside the kiosk flow (a
/// staff device is never asked about a kiosk cart it doesn't have); once the
/// timeout lapses there, with items still in the cart, it pops back to the
/// landing screen and drops the abandoned draft — the same reset
/// KioskConfirmationScreen's "New Order" button performs, just triggered by
/// inactivity instead of a tap.
class KioskIdleGuard extends ConsumerStatefulWidget {
  const KioskIdleGuard({super.key, required this.router, required this.child});

  final GoRouter router;
  final Widget child;

  @override
  ConsumerState<KioskIdleGuard> createState() => _KioskIdleGuardState();
}

class _KioskIdleGuardState extends ConsumerState<KioskIdleGuard> {
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _scheduleCheck();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  void _scheduleCheck() {
    _timer?.cancel();
    _timer = Timer(kioskIdleResetDuration, _onIdle);
  }

  void _onTouch([PointerDownEvent? _]) => _scheduleCheck();

  Future<void> _onIdle() async {
    final location =
        widget.router.routerDelegate.currentConfiguration.uri.path;
    if (!location.startsWith('/kiosk')) {
      // Not in the kiosk flow at all — nothing to abandon, and no kiosk
      // cart to even ask about.
      _scheduleCheck();
      return;
    }

    final cart = ref.read(kioskCartNotifierProvider).valueOrNull;
    if (cart == null || cart.lines.isEmpty) {
      _scheduleCheck();
      return;
    }

    final navigator = widget.router.routerDelegate.navigatorKey.currentState;
    await ref.read(kioskCartNotifierProvider.notifier).resetForIdleTimeout();
    // The matched location is already '/kiosk' (checked above), so the base
    // page IS the landing screen — only the screens pushed imperatively on
    // top of it (menu, cart, fulfillment...) need to come off. Popping back
    // to it, rather than pushing a fresh one, also avoids Navigator's
    // "page-based route cannot be removed imperatively" assertion, which a
    // pushAndRemoveUntil that reaches down to go_router's own root page runs
    // straight into.
    navigator?.popUntil((route) => route.isFirst);
    _scheduleCheck();
  }

  @override
  Widget build(BuildContext context) {
    return Listener(
      behavior: HitTestBehavior.translucent,
      onPointerDown: _onTouch,
      child: widget.child,
    );
  }
}
