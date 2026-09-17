import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'payment_screen.dart';

/// Kiosk orders submitted and awaiting payment at the counter (Purch.io
/// kiosk deliberately has no payment step of its own — see the kiosk cart
/// repository's doc comments). A cashier picks one up here, which claims it
/// as this device's active cart, then pays it through the normal checkout.
class PendingKioskOrdersScreen extends ConsumerWidget {
  const PendingKioskOrdersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ordersAsync = ref.watch(pendingKioskOrdersProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Pending Kiosk Orders'),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.invalidate(pendingKioskOrdersProvider),
          ),
        ],
      ),
      body: ordersAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorStateView(
          message: '$error',
          onRetry: () => ref.invalidate(pendingKioskOrdersProvider),
        ),
        data: (orders) {
          if (orders.isEmpty) {
            return const EmptyStateView(
              icon: Icons.storefront_outlined,
              title: 'No pending kiosk orders',
              description: 'Orders placed at the kiosk will appear here until a cashier collects payment.',
            );
          }

          return ListView.separated(
            padding: const EdgeInsets.all(AppSpacing.lg),
            itemCount: orders.length,
            separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
            itemBuilder: (context, index) => _PendingOrderCard(order: orders[index]),
          );
        },
      ),
    );
  }
}

class _PendingOrderCard extends ConsumerStatefulWidget {
  const _PendingOrderCard({required this.order});

  final Transaction order;

  @override
  ConsumerState<_PendingOrderCard> createState() => _PendingOrderCardState();
}

class _PendingOrderCardState extends ConsumerState<_PendingOrderCard> {
  bool _claiming = false;

  Future<void> _claim() async {
    setState(() => _claiming = true);
    final succeeded = await ref
        .read(cartNotifierProvider.notifier)
        .claimKioskOrder(widget.order.id);
    if (!mounted) return;
    setState(() => _claiming = false);

    if (succeeded) {
      ref.invalidate(pendingKioskOrdersProvider);
      await Navigator.of(context).push<void>(
        MaterialPageRoute(
          builder: (_) => PaymentScreen(total: widget.order.totalAmount),
        ),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Could not claim this order. It may have already been picked up.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final order = widget.order;

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: AppColors.brandPrimaryContainer,
              borderRadius: AppRadius.smBorder,
            ),
            child: Text(
              '${order.kioskPrepNumber ?? '-'}',
              style: const TextStyle(fontWeight: FontWeight.w800, color: AppColors.brandPrimary),
            ),
          ),
          const SizedBox(width: AppSpacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${order.itemCount} item${order.itemCount == 1 ? '' : 's'}'
                  '${order.orderType != null ? ' • ${order.orderType}' : ''}',
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 2),
                Text(
                  '₱${order.totalAmount.toStringAsFixed(2)}',
                  style: const TextStyle(color: AppColors.textSecondary),
                ),
              ],
            ),
          ),
          FilledButton(
            onPressed: _claiming ? null : _claim,
            child: _claiming
                ? const SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Collect Payment'),
          ),
        ],
      ),
    );
  }
}
