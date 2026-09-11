import 'package:flutter/material.dart';

import '../../../pos/domain/transaction_models.dart';
import 'kiosk_landing_screen.dart';

/// E6 — the order/prep number confirmation. A dead end by design (PopScope
/// blocks back navigation, same "no way back into a finished cart" pattern
/// as the cashier POS's ReceiptScreen): the only way out is "New Order",
/// which returns to the landing screen for the next customer.
class KioskConfirmationScreen extends StatelessWidget {
  const KioskConfirmationScreen({super.key, required this.order});

  final Transaction order;

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      child: Scaffold(
        body: SafeArea(
          child: Center(
            child: Padding(
              padding: const EdgeInsets.all(32),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.check_circle, size: 96, color: Colors.green),
                  const SizedBox(height: 24),
                  Text(
                    'Order Submitted!',
                    style: Theme.of(context).textTheme.headlineMedium,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 16),
                  Text(
                    'Your order number is',
                    style: Theme.of(context).textTheme.titleMedium,
                    textAlign: TextAlign.center,
                  ),
                  Text(
                    '${order.kioskPrepNumber ?? '—'}',
                    style: Theme.of(context).textTheme.displayLarge,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Please show this number and pay at the counter to '
                    'complete your order.',
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 48),
                  SizedBox(
                    height: 72,
                    width: double.infinity,
                    child: FilledButton(
                      onPressed:
                          () => Navigator.of(context).pushAndRemoveUntil<void>(
                            MaterialPageRoute(
                              builder: (_) => const KioskLandingScreen(),
                            ),
                            (route) => false,
                          ),
                      child: const Text(
                        'New Order',
                        style: TextStyle(fontSize: 20),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
