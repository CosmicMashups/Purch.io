import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_fonts/google_fonts.dart';

import '../../../pos/domain/transaction_models.dart';
import '../providers/kitchen_display_providers.dart';

/// Full-screen, unattended display for a paired Kitchen Display terminal:
/// pending orders laid out as tickets with their line items, for kitchen
/// staff to prepare against, the same "ticket rail" fast-food kitchens use.
/// Polls the branch's pending-kiosk-orders queue every few seconds.
class KitchenDisplayScreen extends ConsumerStatefulWidget {
  const KitchenDisplayScreen({super.key});

  @override
  ConsumerState<KitchenDisplayScreen> createState() => _KitchenDisplayScreenState();
}

class _KitchenDisplayScreenState extends ConsumerState<KitchenDisplayScreen> {
  Timer? _pollTimer;

  @override
  void initState() {
    super.initState();
    _pollTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      ref.invalidate(kitchenDisplayPendingOrdersProvider);
    });
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final ordersAsync = ref.watch(kitchenDisplayPendingOrdersProvider);

    return Scaffold(
      backgroundColor: const Color(0xFF0B0F19),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(32, 24, 32, 12),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    'Kitchen Tickets',
                    style: GoogleFonts.outfit(
                      fontSize: 28,
                      fontWeight: FontWeight.w800,
                      color: Colors.white,
                    ),
                  ),
                  ordersAsync.maybeWhen(
                    data: (orders) => Container(
                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                      decoration: BoxDecoration(
                        color: const Color(0x2010B981),
                        borderRadius: BorderRadius.circular(999),
                        border: Border.all(color: const Color(0xFF10B981).withValues(alpha: 0.5)),
                      ),
                      child: Text(
                        '${orders.length} pending',
                        style: GoogleFonts.plusJakartaSans(
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                          color: const Color(0xFF10B981),
                        ),
                      ),
                    ),
                    orElse: () => const SizedBox.shrink(),
                  ),
                ],
              ),
            ),
            Expanded(
              child: ordersAsync.when(
                loading: () => const Center(child: CircularProgressIndicator(color: Colors.white38)),
                error: (error, _) => Center(
                  child: Text(
                    'Could not load pending orders.',
                    style: GoogleFonts.plusJakartaSans(color: Colors.white54, fontSize: 18),
                  ),
                ),
                data: (orders) {
                  if (orders.isEmpty) {
                    return Center(
                      child: Text(
                        'No pending orders',
                        style: GoogleFonts.outfit(
                          fontSize: 32,
                          fontWeight: FontWeight.w700,
                          color: Colors.white24,
                        ),
                      ),
                    );
                  }

                  return GridView.builder(
                    padding: const EdgeInsets.all(24),
                    gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                      maxCrossAxisExtent: 320,
                      mainAxisSpacing: 16,
                      crossAxisSpacing: 16,
                      childAspectRatio: 0.78,
                    ),
                    itemCount: orders.length,
                    itemBuilder: (context, index) => _TicketCard(order: orders[index]),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TicketCard extends ConsumerStatefulWidget {
  const _TicketCard({required this.order});

  final Transaction order;

  @override
  ConsumerState<_TicketCard> createState() => _TicketCardState();
}

class _TicketCardState extends ConsumerState<_TicketCard> {
  bool _updating = false;

  Future<void> _advanceTo(KitchenStatus status) async {
    setState(() => _updating = true);
    await ref
        .read(kitchenStatusControllerProvider.notifier)
        .advance(widget.order.id, status);
    if (mounted) setState(() => _updating = false);
  }

  @override
  Widget build(BuildContext context) {
    final order = widget.order;
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFF131B2E),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: order.kitchenStatus == KitchenStatus.ready
              ? const Color(0xFF10B981)
              : const Color(0xFF1E293B),
          width: order.kitchenStatus == KitchenStatus.ready ? 2 : 1,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            decoration: const BoxDecoration(
              border: Border(bottom: BorderSide(color: Color(0xFF1E293B))),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  '#${order.kioskPrepNumber ?? '-'}',
                  style: GoogleFonts.jetBrainsMono(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    color: Colors.white,
                  ),
                ),
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    if (order.orderType != null) ...[
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                        decoration: BoxDecoration(
                          color: const Color(0xFF1E293B),
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Text(
                          order.orderType!,
                          style: GoogleFonts.plusJakartaSans(
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                            color: Colors.white70,
                          ),
                        ),
                      ),
                      const SizedBox(width: 6),
                    ],
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                      decoration: BoxDecoration(
                        color: _statusColor(order.kitchenStatus).withValues(alpha: 0.15),
                        borderRadius: BorderRadius.circular(999),
                        border: Border.all(color: _statusColor(order.kitchenStatus).withValues(alpha: 0.5)),
                      ),
                      child: Text(
                        _statusLabel(order.kitchenStatus),
                        style: GoogleFonts.plusJakartaSans(
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                          color: _statusColor(order.kitchenStatus),
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          Expanded(
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              itemCount: order.lines.length,
              separatorBuilder: (_, __) => const Divider(color: Color(0x15FFFFFF), height: 1),
              itemBuilder: (context, index) {
                final line = order.lines[index];
                return Padding(
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${line.quantity.toStringAsFixed(line.quantity.truncateToDouble() == line.quantity ? 0 : 2)}x',
                        style: GoogleFonts.jetBrainsMono(
                          fontSize: 14,
                          fontWeight: FontWeight.w700,
                          color: const Color(0xFF10B981),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          line.itemName,
                          style: GoogleFonts.plusJakartaSans(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                            color: Colors.white,
                          ),
                        ),
                      ),
                    ],
                  ),
                );
              },
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(12),
            child: SizedBox(
              width: double.infinity,
              height: 44,
              child: FilledButton(
                onPressed: _updating ? null : _onActionPressed,
                style: FilledButton.styleFrom(
                  backgroundColor: _statusColor(order.kitchenStatus),
                  foregroundColor: Colors.black,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                ),
                child: _updating
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.black54),
                      )
                    : Text(
                        _actionLabel(order.kitchenStatus),
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _onActionPressed() {
    switch (widget.order.kitchenStatus) {
      case KitchenStatus.queued:
        _advanceTo(KitchenStatus.preparing);
      case KitchenStatus.preparing:
        _advanceTo(KitchenStatus.ready);
      case KitchenStatus.ready:
        _advanceTo(KitchenStatus.pickedUp);
      case KitchenStatus.pickedUp:
        break;
    }
  }

  static String _actionLabel(KitchenStatus status) => switch (status) {
    KitchenStatus.queued => 'Start Preparing',
    KitchenStatus.preparing => 'Mark Ready',
    KitchenStatus.ready => 'Picked Up',
    KitchenStatus.pickedUp => 'Done',
  };

  static String _statusLabel(KitchenStatus status) => switch (status) {
    KitchenStatus.queued => 'Queued',
    KitchenStatus.preparing => 'Preparing',
    KitchenStatus.ready => 'Ready',
    KitchenStatus.pickedUp => 'Picked Up',
  };

  static Color _statusColor(KitchenStatus status) => switch (status) {
    KitchenStatus.queued => const Color(0xFF94A3B8),
    KitchenStatus.preparing => const Color(0xFFF59E0B),
    KitchenStatus.ready => const Color(0xFF10B981),
    KitchenStatus.pickedUp => const Color(0xFF64748B),
  };
}
