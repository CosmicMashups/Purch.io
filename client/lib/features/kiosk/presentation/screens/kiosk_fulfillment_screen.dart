import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/kiosk_providers.dart';
import 'kiosk_confirmation_screen.dart';

/// E4 — the fulfillment choice. Picking one sets Transaction.OrderType, then
/// immediately submits the order (E6) — there's nothing else left to choose
/// on a kiosk order once fulfillment is picked, since there's no payment
/// step here at all (see Key Architecture Decisions).
class KioskFulfillmentScreen extends ConsumerStatefulWidget {
  const KioskFulfillmentScreen({super.key});

  @override
  ConsumerState<KioskFulfillmentScreen> createState() =>
      _KioskFulfillmentScreenState();
}

class _KioskFulfillmentScreenState
    extends ConsumerState<KioskFulfillmentScreen> {
  bool _submitting = false;

  Future<void> _chooseAndSubmit(String orderType) async {
    setState(() => _submitting = true);

    final controller = ref.read(kioskCartNotifierProvider.notifier);
    final typeSet = await controller.setOrderType(orderType);
    if (!typeSet) {
      if (mounted) {
        setState(() => _submitting = false);
      }
      return;
    }

    final submitted = await controller.submitOrder();
    if (!mounted) {
      return;
    }

    if (submitted == null) {
      setState(() => _submitting = false);
      final failure = controller.currentFailure;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            failure?.message ?? 'Could not submit your order — try again.',
          ),
        ),
      );
      return;
    }

    await Navigator.of(context).pushAndRemoveUntil<void>(
      MaterialPageRoute(
        builder: (_) => KioskConfirmationScreen(order: submitted),
      ),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('For Here or To Go?')),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child:
              _submitting
                  ? const Center(child: CircularProgressIndicator())
                  : Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      SizedBox(
                        height: 96,
                        width: double.infinity,
                        child: FilledButton.icon(
                          onPressed: () => _chooseAndSubmit('Dine In'),
                          icon: const Icon(Icons.restaurant, size: 32),
                          label: const Text(
                            'Dine In',
                            style: TextStyle(fontSize: 20),
                          ),
                        ),
                      ),
                      const SizedBox(height: 24),
                      SizedBox(
                        height: 96,
                        width: double.infinity,
                        child: FilledButton.icon(
                          onPressed: () => _chooseAndSubmit('Take Out'),
                          icon: const Icon(Icons.shopping_bag, size: 32),
                          label: const Text(
                            'Take Out',
                            style: TextStyle(fontSize: 20),
                          ),
                        ),
                      ),
                    ],
                  ),
        ),
      ),
    );
  }
}
