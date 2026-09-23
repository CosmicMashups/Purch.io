import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_fonts/google_fonts.dart';
import 'cfd_models.dart';
import '../hardware_providers.dart';
import '../../formatting/money.dart';
import '../../theming/app_tokens.dart';

/// Full-screen Customer Facing Display UI designed for secondary HDMI monitors,
/// customer-facing tablets, or dual-screen POS terminals.
class CustomerFacingDisplayScreen extends ConsumerWidget {
  const CustomerFacingDisplayScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cfdStateAsync = ref.watch(cfdStateStreamProvider);
    final cfdState = cfdStateAsync.valueOrNull ?? ref.read(cfdServiceProvider).currentState;

    return Scaffold(
      backgroundColor: const Color(0xFF0A0E1A),
      body: SafeArea(
        child: Column(
          children: [
            // --- TOP HEADER ---
            _buildHeader(context, cfdState),

            // --- MAIN SPLIT BODY ---
            Expanded(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // LEFT: Live Cart Line Items (60%)
                  Expanded(
                    flex: 6,
                    child: _buildItemsList(cfdState),
                  ),

                  // RIGHT: Totals & Dynamic QR Ph (40%)
                  Expanded(
                    flex: 4,
                    child: _buildSummaryPanel(cfdState),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildHeader(BuildContext context, CfdState state) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 18),
      decoration: const BoxDecoration(
        color: Color(0xFF131A2C),
        border: Border(bottom: BorderSide(color: Color(0xFF1E293B))),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              Container(
                width: 42,
                height: 42,
                decoration: BoxDecoration(
                  color: AppColors.brandPrimary,
                  borderRadius: BorderRadius.circular(10),
                ),
                alignment: Alignment.center,
                child: const Text(
                  'P.',
                  style: TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w900,
                    fontSize: 20,
                  ),
                ),
              ),
              const SizedBox(width: 16),
              Text(
                state.storeName,
                style: GoogleFonts.outfit(
                  fontSize: 24,
                  fontWeight: FontWeight.w800,
                  color: Colors.white,
                  letterSpacing: -0.5,
                ),
              ),
            ],
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
            decoration: BoxDecoration(
              color: const Color(0x2010B981),
              borderRadius: BorderRadius.circular(999),
              border: Border.all(color: AppColors.accentEmerald.withValues(alpha: 0.5)),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  width: 8,
                  height: 8,
                  decoration: const BoxDecoration(
                    color: AppColors.accentEmerald,
                    shape: BoxShape.circle,
                  ),
                ),
                const SizedBox(width: 8),
                Text(
                  state.mode == CfdMode.payment ? 'AWAITING PAYMENT' : 'LIVE REGISTER',
                  style: GoogleFonts.plusJakartaSans(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: AppColors.accentEmerald,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildItemsList(CfdState state) {
    if (state.lines.isEmpty) {
      return Container(
        margin: const EdgeInsets.all(24),
        decoration: BoxDecoration(
          color: const Color(0xFF131A2C),
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: const Color(0xFF1E293B)),
        ),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.shopping_basket_outlined,
                size: 72,
                color: Color(0xFF475569),
              ),
              const SizedBox(height: 16),
              Text(
                state.welcomeMessage,
                style: GoogleFonts.outfit(
                  fontSize: 28,
                  fontWeight: FontWeight.w800,
                  color: Colors.white,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                'Your items will appear here as they are scanned.',
                style: GoogleFonts.plusJakartaSans(
                  fontSize: 16,
                  color: const Color(0xFF94A3B8),
                ),
              ),
            ],
          ),
        ),
      );
    }

    return Container(
      margin: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: const Color(0xFF131A2C),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: const Color(0xFF1E293B)),
      ),
      child: Column(
        children: [
          // Table header
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
            decoration: const BoxDecoration(
              border: Border(bottom: BorderSide(color: Color(0xFF1E293B))),
            ),
            child: Row(
              children: [
                Expanded(
                  flex: 5,
                  child: Text(
                    'ITEM DESCRIPTION',
                    style: GoogleFonts.plusJakartaSans(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: const Color(0xFF94A3B8),
                    ),
                  ),
                ),
                Expanded(
                  flex: 2,
                  child: Text(
                    'QTY',
                    textAlign: TextAlign.center,
                    style: GoogleFonts.plusJakartaSans(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: const Color(0xFF94A3B8),
                    ),
                  ),
                ),
                Expanded(
                  flex: 3,
                  child: Text(
                    'AMOUNT',
                    textAlign: TextAlign.right,
                    style: GoogleFonts.plusJakartaSans(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: const Color(0xFF94A3B8),
                    ),
                  ),
                ),
              ],
            ),
          ),
          // Items list
          Expanded(
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
              itemCount: state.lines.length,
              separatorBuilder: (_, __) =>
                  const Divider(color: Color(0x15FFFFFF), height: 1),
              itemBuilder: (context, index) {
                final line = state.lines[index];
                return Padding(
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  child: Row(
                    children: [
                      Expanded(
                        flex: 5,
                        child: Text(
                          line.name,
                          style: GoogleFonts.plusJakartaSans(
                            fontSize: 18,
                            fontWeight: FontWeight.w600,
                            color: Colors.white,
                          ),
                        ),
                      ),
                      Expanded(
                        flex: 2,
                        child: Text(
                          '×${line.quantity.toStringAsFixed(line.quantity.truncateToDouble() == line.quantity ? 0 : 2)}',
                          textAlign: TextAlign.center,
                          style: GoogleFonts.jetBrainsMono(
                            fontSize: 16,
                            color: const Color(0xFF94A3B8),
                          ),
                        ),
                      ),
                      Expanded(
                        flex: 3,
                        child: Text(
                          formatCurrency(line.lineTotal),
                          textAlign: TextAlign.right,
                          style: GoogleFonts.jetBrainsMono(
                            fontSize: 18,
                            fontWeight: FontWeight.w700,
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
        ],
      ),
    );
  }

  Widget _buildSummaryPanel(CfdState state) {
    return Container(
      margin: const EdgeInsets.only(top: 24, bottom: 24, right: 24),
      padding: const EdgeInsets.all(28),
      decoration: BoxDecoration(
        color: const Color(0xFF131A2C),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: const Color(0xFF1E293B)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Subtotal
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Subtotal',
                style: GoogleFonts.plusJakartaSans(
                  fontSize: 16,
                  color: const Color(0xFF94A3B8),
                ),
              ),
              Text(
                formatCurrency(state.subtotal),
                style: GoogleFonts.jetBrainsMono(
                  fontSize: 18,
                  fontWeight: FontWeight.w600,
                  color: Colors.white,
                ),
              ),
            ],
          ),

          if (state.discountAmount > 0) ...[
            const SizedBox(height: 12),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Savings & Discounts',
                  style: GoogleFonts.plusJakartaSans(
                    fontSize: 16,
                    color: AppColors.accentEmerald,
                  ),
                ),
                Text(
                  '-${formatCurrency(state.discountAmount)}',
                  style: GoogleFonts.jetBrainsMono(
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                    color: AppColors.accentEmerald,
                  ),
                ),
              ],
            ),
          ],

          const SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'VATable Sales',
                style: GoogleFonts.plusJakartaSans(
                  fontSize: 14,
                  color: const Color(0xFF94A3B8),
                ),
              ),
              Text(
                formatCurrency(state.vatableSales),
                style: GoogleFonts.jetBrainsMono(
                  fontSize: 14,
                  color: const Color(0xFF94A3B8),
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'VAT (12%)',
                style: GoogleFonts.plusJakartaSans(
                  fontSize: 14,
                  color: const Color(0xFF94A3B8),
                ),
              ),
              Text(
                formatCurrency(state.vatAmount),
                style: GoogleFonts.jetBrainsMono(
                  fontSize: 14,
                  color: const Color(0xFF94A3B8),
                ),
              ),
            ],
          ),

          const SizedBox(height: 20),

          // Total Box
          Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
              color: const Color(0x1510B981),
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: const Color(0x4010B981)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(
                  'TOTAL TO PAY (VAT INCLUSIVE)',
                  style: GoogleFonts.plusJakartaSans(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: const Color(0xFF94A3B8),
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  formatCurrency(state.totalAmount),
                  style: GoogleFonts.jetBrainsMono(
                    fontSize: 42,
                    fontWeight: FontWeight.w800,
                    color: AppColors.accentEmerald,
                  ),
                ),
              ],
            ),
          ),

          const Spacer(),

          // Dynamic QR Ph or Change Display
          if (state.qrPhPayload != null && state.qrPhPayload!.isNotEmpty) ...[
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
              ),
              child: Column(
                children: [
                  Image.network(
                    'https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${Uri.encodeComponent(state.qrPhPayload!)}',
                    width: 170,
                    height: 170,
                    errorBuilder: (_, __, ___) => const SizedBox(
                      height: 170,
                      child: Center(
                        child: Text(
                          'QR Code Ready',
                          style: TextStyle(color: Colors.black),
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Scan with GCash, Maya, or any Bank App',
                    style: TextStyle(
                      color: Color(0xFF0F172A),
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                    ),
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            ),
          ] else if (state.changeGiven != null && state.changeGiven! > 0) ...[
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: const Color(0x203B82F6),
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: const Color(0xFF3B82F6)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  const Text(
                    'CHANGE DUE',
                    style: TextStyle(
                      color: Color(0xFF94A3B8),
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  Text(
                    formatCurrency(state.changeGiven!),
                    style: GoogleFonts.jetBrainsMono(
                      fontSize: 32,
                      fontWeight: FontWeight.w800,
                      color: const Color(0xFF60A5FA),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}
