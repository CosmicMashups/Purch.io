import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'kiosk_category_screen.dart';

/// E1 — the kiosk's idle/landing screen. Portrait-only, separate from the
/// landscape staff app shell entirely (see PurchApp's startup routing) —
/// a customer walks up, taps to start, and everything from here on is
/// order-building only, never payment (see Key Architecture Decisions).
class KioskLandingScreen extends StatefulWidget {
  const KioskLandingScreen({super.key});

  @override
  State<KioskLandingScreen> createState() => _KioskLandingScreenState();
}

class _KioskLandingScreenState extends State<KioskLandingScreen> {
  @override
  void initState() {
    super.initState();
    unawaited(
      SystemChrome.setPreferredOrientations([
        DeviceOrientation.portraitUp,
        DeviceOrientation.portraitDown,
      ]),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.storefront, size: 96),
                const SizedBox(height: 24),
                Text(
                  'Welcome!',
                  style: Theme.of(context).textTheme.headlineLarge,
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 8),
                const Text(
                  'Tap below to start your order.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 48),
                SizedBox(
                  height: 72,
                  width: double.infinity,
                  child: FilledButton(
                    onPressed:
                        () => Navigator.of(context).push<void>(
                          MaterialPageRoute(
                            builder: (_) => const KioskCategoryScreen(),
                          ),
                        ),
                    child: const Text(
                      'Start Order',
                      style: TextStyle(fontSize: 20),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
