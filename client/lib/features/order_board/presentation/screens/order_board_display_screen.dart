import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_fonts/google_fonts.dart';

import '../../../pos/domain/transaction_models.dart';
import '../providers/order_board_providers.dart';

/// Full-screen, unattended display for a paired Order Number Board —
/// customers glance at this to see which orders are ready/pending while
/// waiting at the counter, the same "call out the number" pattern fast-food
/// counters use. Polls the branch's pending-kiosk-orders queue every few
/// seconds; no WebSocket server needed for a board this simple.
class OrderBoardDisplayScreen extends ConsumerStatefulWidget {
  const OrderBoardDisplayScreen({super.key});

  @override
  ConsumerState<OrderBoardDisplayScreen> createState() => _OrderBoardDisplayScreenState();
}

class _OrderBoardDisplayScreenState extends ConsumerState<OrderBoardDisplayScreen> {
  Timer? _pollTimer;

  @override
  void initState() {
    super.initState();
    _pollTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      ref.invalidate(orderBoardPendingOrdersProvider);
    });
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final ordersAsync = ref.watch(orderBoardPendingOrdersProvider);

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
                    'Order Status',
                    style: GoogleFonts.outfit(
                      fontSize: 28,
                      fontWeight: FontWeight.w800,
                      color: Colors.white,
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                    decoration: BoxDecoration(
                      color: const Color(0x2010B981),
                      borderRadius: BorderRadius.circular(999),
                      border: Border.all(color: const Color(0xFF10B981).withValues(alpha: 0.5)),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Icons.circle, size: 8, color: Color(0xFF10B981)),
                        const SizedBox(width: 8),
                        Text(
                          'LIVE',
                          style: GoogleFonts.plusJakartaSans(
                            fontSize: 12,
                            fontWeight: FontWeight.w700,
                            color: const Color(0xFF10B981),
                          ),
                        ),
                      ],
                    ),
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

                  final ready = orders.where((o) => o.kitchenStatus == KitchenStatus.ready).toList();
                  final preparing = orders.where((o) => o.kitchenStatus != KitchenStatus.ready).toList();

                  return ListView(
                    padding: const EdgeInsets.all(32),
                    children: [
                      if (ready.isNotEmpty) ...[
                        _SectionLabel(label: 'Ready for Pickup', color: const Color(0xFF10B981)),
                        const SizedBox(height: 16),
                        _NumberGrid(orders: ready, color: const Color(0xFF10B981)),
                        const SizedBox(height: 32),
                      ],
                      if (preparing.isNotEmpty) ...[
                        _SectionLabel(label: 'Preparing', color: const Color(0xFF94A3B8)),
                        const SizedBox(height: 16),
                        _NumberGrid(orders: preparing, color: const Color(0xFF1E293B)),
                      ],
                    ],
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

class _SectionLabel extends StatelessWidget {
  const _SectionLabel({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(width: 4, height: 20, color: color),
        const SizedBox(width: 10),
        Text(
          label,
          style: GoogleFonts.outfit(
            fontSize: 20,
            fontWeight: FontWeight.w700,
            color: Colors.white,
          ),
        ),
      ],
    );
  }
}

class _NumberGrid extends StatelessWidget {
  const _NumberGrid({required this.orders, required this.color});

  final List<Transaction> orders;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
        maxCrossAxisExtent: 220,
        mainAxisSpacing: 20,
        crossAxisSpacing: 20,
        childAspectRatio: 1.2,
      ),
      itemCount: orders.length,
      itemBuilder: (context, index) {
        final order = orders[index];
        return Container(
          decoration: BoxDecoration(
            color: const Color(0xFF131B2E),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: color, width: color == const Color(0xFF1E293B) ? 1 : 2),
          ),
          alignment: Alignment.center,
          child: Text(
            '${order.kioskPrepNumber ?? '-'}',
            style: GoogleFonts.jetBrainsMono(
              fontSize: 56,
              fontWeight: FontWeight.w800,
              color: Colors.white,
            ),
          ),
        );
      },
    );
  }
}
