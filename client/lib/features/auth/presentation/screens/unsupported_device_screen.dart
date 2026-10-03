import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/session/session_scope.dart';
import '../../../../core/theming/app_tokens.dart';
import '../providers/auth_providers.dart';

/// Shown on a device paired as a type this app does not run (the Customer
/// Display runs in the web app). Lets the device be unpaired and paired again.
class UnsupportedDeviceScreen extends ConsumerWidget {
  const UnsupportedDeviceScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'This device type runs in the web app',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
                ),
                const SizedBox(height: 8),
                const Text(
                  'A Customer Display is shown from the Purch.io web app. Open it in a browser on this screen, or pair this device as something else.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: AppColors.textSecondary),
                ),
                const SizedBox(height: 20),
                FilledButton(
                  onPressed: () async {
                    await ref.read(authRepositoryProvider).unpair();
                    resetSessionScope();
                  },
                  child: const Text('Unpair this device'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
