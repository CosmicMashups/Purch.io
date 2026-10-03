import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../domain/auth_models.dart';
import '../providers/auth_providers.dart';

/// The lock screen of a paired Register or Warehouse device. A person picks
/// their own name and types their own PIN; a few wrong PINs lock that person
/// out for a while (the server counts them). Nobody's password is ever typed here.
class UnlockScreen extends ConsumerStatefulWidget {
  const UnlockScreen({super.key, required this.onUnlocked});

  /// Called once someone is unlocked, so the app can reset and route by their role.
  final VoidCallback onUnlocked;

  @override
  ConsumerState<UnlockScreen> createState() => _UnlockScreenState();
}

class _UnlockScreenState extends ConsumerState<UnlockScreen> {
  RosterPerson? _person;
  String _pin = '';

  void _press(String digit) {
    if (_pin.length >= 8) {
      return;
    }
    setState(() => _pin += digit);
  }

  void _backspace() {
    if (_pin.isEmpty) {
      return;
    }
    setState(() => _pin = _pin.substring(0, _pin.length - 1));
  }

  Future<void> _submit() async {
    final person = _person;
    if (person == null || _pin.length < 4) {
      return;
    }

    final unlocked = await ref
        .read(unlockControllerProvider.notifier)
        .unlock(membershipId: person.membershipId, pin: _pin);

    if (!mounted) {
      return;
    }
    if (unlocked) {
      widget.onUnlocked();
    } else {
      setState(() => _pin = '');
    }
  }

  Future<void> _pairAgain() async {
    await ref.read(authRepositoryProvider).unpair();
    if (mounted) {
      widget.onUnlocked();
    }
  }

  @override
  Widget build(BuildContext context) {
    final roster = ref.watch(deviceRosterProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: roster.when(
                loading: () => const CircularProgressIndicator(),
                error:
                    (error, _) => _RosterError(
                      message: describeError(error),
                      onRetry: () => ref.invalidate(deviceRosterProvider),
                      onPairAgain: _pairAgain,
                    ),
                data: (value) => _buildContent(context, value),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildContent(BuildContext context, DeviceRoster roster) {
    final unlock = ref.watch(unlockControllerProvider);
    final failure = ref.read(unlockControllerProvider.notifier).currentFailure;
    final person = _person;

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          roster.deviceName ?? 'This device is locked',
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontSize: 22,
            fontWeight: FontWeight.w800,
            color: AppColors.textPrimary,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          person == null
              ? 'Who is working? Tap your name.'
              : 'Hi ${person.name}. Type your PIN.',
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 14, color: AppColors.textSecondary),
        ),
        const SizedBox(height: 20),
        if (person == null) ...[
          if (roster.people.isEmpty)
            const Text(
              'Nobody is set up to work on this device yet. Ask an admin to invite you.',
              textAlign: TextAlign.center,
              style: TextStyle(color: AppColors.textSecondary),
            ),
          for (final entry in roster.people)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: OutlinedButton(
                onPressed:
                    entry.hasPin
                        ? () => setState(() {
                          _person = entry;
                          _pin = '';
                        })
                        : null,
                child: Text(
                  entry.hasPin ? entry.name : '${entry.name} (no PIN set)',
                ),
              ),
            ),
        ] else ...[
          _PinDots(length: _pin.length),
          if (failure != null) ...[
            const SizedBox(height: 12),
            Text(
              failure.message,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 13,
                color: AppColors.error,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
          const SizedBox(height: 16),
          _PinPad(
            enabled: !unlock.isLoading,
            onDigit: _press,
            onBackspace: _backspace,
          ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: unlock.isLoading || _pin.length < 4 ? null : _submit,
            child:
                unlock.isLoading
                    ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                    : const Text('Unlock'),
          ),
          TextButton(
            onPressed:
                () => setState(() {
                  _person = null;
                  _pin = '';
                }),
            child: const Text('Not you?'),
          ),
        ],
        const SizedBox(height: 8),
        TextButton(
          onPressed: () => context.go('/login'),
          child: const Text('Sign in with email instead'),
        ),
      ],
    );
  }
}

class _RosterError extends StatelessWidget {
  const _RosterError({
    required this.message,
    required this.onRetry,
    required this.onPairAgain,
  });

  final String message;
  final VoidCallback onRetry;
  final VoidCallback onPairAgain;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'This device cannot be unlocked right now',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 8),
        Text(
          message,
          textAlign: TextAlign.center,
          style: const TextStyle(color: AppColors.textSecondary),
        ),
        const SizedBox(height: 16),
        FilledButton(onPressed: onRetry, child: const Text('Try again')),
        TextButton(
          onPressed: onPairAgain,
          child: const Text('Pair this device again'),
        ),
      ],
    );
  }
}

class _PinDots extends StatelessWidget {
  const _PinDots({required this.length});

  final int length;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: '$length digits entered',
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          for (var i = 0; i < (length < 4 ? 4 : length); i++)
            Container(
              width: 14,
              height: 14,
              margin: const EdgeInsets.symmetric(horizontal: 6),
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: i < length ? AppColors.brandPrimary : Colors.transparent,
                border: Border.all(color: AppColors.brandPrimary, width: 2),
              ),
            ),
        ],
      ),
    );
  }
}

class _PinPad extends StatelessWidget {
  const _PinPad({
    required this.enabled,
    required this.onDigit,
    required this.onBackspace,
  });

  final bool enabled;
  final ValueChanged<String> onDigit;
  final VoidCallback onBackspace;

  @override
  Widget build(BuildContext context) {
    Widget key(String label, VoidCallback? onTap, {Widget? child}) {
      return Padding(
        padding: const EdgeInsets.all(4),
        child: SizedBox(
          height: 64,
          child: OutlinedButton(
            onPressed: enabled ? onTap : null,
            child: child ?? Text(label, style: const TextStyle(fontSize: 22)),
          ),
        ),
      );
    }

    return Column(
      children: [
        for (final row in const [
          ['1', '2', '3'],
          ['4', '5', '6'],
          ['7', '8', '9'],
        ])
          Row(
            children: [
              for (final digit in row)
                Expanded(child: key(digit, () => onDigit(digit))),
            ],
          ),
        Row(
          children: [
            const Expanded(child: SizedBox()),
            Expanded(child: key('0', () => onDigit('0'))),
            Expanded(
              child: key(
                'Backspace',
                onBackspace,
                child: const Icon(Icons.backspace_outlined),
              ),
            ),
          ],
        ),
      ],
    );
  }
}
