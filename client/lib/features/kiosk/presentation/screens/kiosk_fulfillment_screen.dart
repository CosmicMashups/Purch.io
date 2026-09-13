import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../providers/kiosk_providers.dart';
import 'kiosk_confirmation_screen.dart';

/// E4 — Fulfillment choice screen (Dine In vs Take Out).
/// Tactile selection tiles with clear active feedback and submission state.
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
          behavior: SnackBarBehavior.floating,
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
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
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('For Here or To Go?'),
        backgroundColor: AppColors.surface,
        centerTitle: true,
        elevation: 0,
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child:
              _submitting
                  ? Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const CircularProgressIndicator(
                          color: AppColors.brandPrimary,
                        ),
                        const SizedBox(height: 20),
                        const Text(
                          'Submitting your order...',
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w600,
                            color: AppColors.textPrimary,
                          ),
                        ),
                      ],
                    ),
                  )
                  : Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      _FulfillmentCard(
                        title: 'Dine In',
                        subtitle: 'Enjoy your food inside the store',
                        icon: Icons.restaurant_rounded,
                        onTap: () => _chooseAndSubmit('Dine In'),
                      ),
                      const SizedBox(height: 24),
                      _FulfillmentCard(
                        title: 'Take Out',
                        subtitle: 'Pack for takeaway',
                        icon: Icons.shopping_bag_rounded,
                        onTap: () => _chooseAndSubmit('Take Out'),
                      ),
                    ],
                  ),
        ),
      ),
    );
  }
}

class _FulfillmentCard extends StatelessWidget {
  const _FulfillmentCard({
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.onTap,
  });

  final String title;
  final String subtitle;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surface,
      borderRadius: AppRadius.lgBorder,
      child: InkWell(
        borderRadius: AppRadius.lgBorder,
        onTap: onTap,
        child: Container(
          decoration: BoxDecoration(
            borderRadius: AppRadius.lgBorder,
            border: Border.all(color: AppColors.border, width: 1.5),
            boxShadow: AppShadows.card,
          ),
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 28),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: AppColors.brandPrimaryContainer,
                  borderRadius: BorderRadius.circular(AppRadius.md),
                ),
                child: Icon(icon, size: 36, color: AppColors.brandPrimary),
              ),
              const SizedBox(width: 20),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: const TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                        letterSpacing: -0.3,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      subtitle,
                      style: const TextStyle(
                        fontSize: 14,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(
                Icons.arrow_forward_ios_rounded,
                size: 20,
                color: AppColors.textMuted,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
