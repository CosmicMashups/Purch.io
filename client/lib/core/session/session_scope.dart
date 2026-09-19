import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Bumped whenever the signed-in identity changes (login, logout, expired
/// session). [SessionScope] keys its [ProviderScope] on this, so every
/// provider — and every `keepAlive` cache of the previous tenant's data — is
/// disposed in one step instead of surviving into the next session.
final ValueNotifier<int> sessionEpoch = ValueNotifier<int>(0);

/// Discards all in-memory app state. Call after tokens change hands (login,
/// logout, session expiry) so nothing from the previous session stays visible.
void resetSessionScope() {
  sessionEpoch.value++;
}

/// Wraps the app in a [ProviderScope] that is rebuilt from scratch whenever
/// [resetSessionScope] is called.
class SessionScope extends StatelessWidget {
  const SessionScope({required this.child, super.key});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<int>(
      valueListenable: sessionEpoch,
      builder:
          (context, epoch, _) => ProviderScope(
            key: ValueKey<int>(epoch),
            child: child,
          ),
    );
  }
}
