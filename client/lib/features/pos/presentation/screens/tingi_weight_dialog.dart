import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_fonts/google_fonts.dart';

import '../../../../core/formatting/money.dart';
import '../../../../core/hardware/hardware_providers.dart';
import '../../../../core/hardware/scale/scale_driver.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_models.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';

/// Commercial Tingi / Weight dialog for groceries, meat shops, and dampa/palengke stalls.
/// Polls live weight from physical or simulated RS232 scales, shows stability,
/// allows Tare/Zero and preset Tingi portions, and adds the weighed line directly to the cart.
class TingiWeightDialog extends ConsumerStatefulWidget {
  const TingiWeightDialog({
    super.key,
    required this.item,
    this.onAdded,
  });

  final Item item;
  final VoidCallback? onAdded;

  @override
  ConsumerState<TingiWeightDialog> createState() => _TingiWeightDialogState();
}

class _TingiWeightDialogState extends ConsumerState<TingiWeightDialog> {
  final TextEditingController _manualController = TextEditingController();
  bool _isManualEntry = false;
  bool _isSubmitting = false;

  @override
  void dispose() {
    _manualController.dispose();
    super.dispose();
  }

  Future<void> _submitWeight(double weight) async {
    if (weight <= 0 || _isSubmitting) return;

    setState(() => _isSubmitting = true);
    final controller = ref.read(cartNotifierProvider.notifier);

    final succeeded = await controller.addLine(
      AddTransactionLineRequest(
        itemId: widget.item.id,
        quantity: weight,
      ),
    );

    if (mounted) {
      setState(() => _isSubmitting = false);
      if (succeeded) {
        widget.onAdded?.call();
        Navigator.of(context).pop(true);
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Failed to add weighed item to cart.')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final scaleReadingAsync = ref.watch(scaleReadingStreamProvider);
    final scaleService = ref.read(scaleServiceProvider);

    final scaleReading = scaleReadingAsync.valueOrNull ?? scaleService.latestReading;
    final activeWeight = _isManualEntry
        ? (double.tryParse(_manualController.text) ?? 0.0)
        : scaleReading.weight;

    final lineTotal = activeWeight * widget.item.basePrice;
    final canSubmit = activeWeight > 0 &&
        !_isSubmitting &&
        (_isManualEntry || (scaleReading.isStable && !scaleReading.isOverload));

    return Dialog(
      backgroundColor: AppColors.surface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      insetPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // --- HEADER ---
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          widget.item.name,
                          style: GoogleFonts.outfit(
                            fontSize: 20,
                            fontWeight: FontWeight.w800,
                            color: AppColors.textPrimary,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                        const SizedBox(height: 2),
                        Text(
                          '${formatCurrency(widget.item.basePrice)} / kg',
                          style: GoogleFonts.jetBrainsMono(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                            color: AppColors.brandPrimary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  IconButton(
                    onPressed: () => Navigator.of(context).pop(),
                    icon: const Icon(Icons.close_rounded, color: AppColors.textSecondary),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              const Divider(color: AppColors.border, height: 1),
              const SizedBox(height: 20),

              // --- DIGITAL SCALE READOUT ---
              Container(
                padding: const EdgeInsets.symmetric(vertical: 20, horizontal: 24),
                decoration: BoxDecoration(
                  color: const Color(0xFF0F172A), // High contrast dark VFD meter
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(
                    color: scaleReading.isStable
                        ? AppColors.accentEmerald.withValues(alpha: 0.5)
                        : Colors.amber.withValues(alpha: 0.5),
                    width: 2,
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: (scaleReading.isStable ? AppColors.accentEmerald : Colors.amber)
                          .withValues(alpha: 0.15),
                      blurRadius: 16,
                      spreadRadius: 2,
                    ),
                  ],
                ),
                child: Column(
                  children: [
                    // Status Badge (Stable / Motion / Overload)
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: scaleReading.isOverload
                                ? Colors.red.withValues(alpha: 0.2)
                                : scaleReading.isStable
                                    ? AppColors.accentEmerald.withValues(alpha: 0.2)
                                    : Colors.amber.withValues(alpha: 0.2),
                            borderRadius: BorderRadius.circular(AppRadius.full),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Container(
                                width: 8,
                                height: 8,
                                decoration: BoxDecoration(
                                  color: scaleReading.isOverload
                                      ? Colors.red
                                      : scaleReading.isStable
                                          ? AppColors.accentEmerald
                                          : Colors.amber,
                                  shape: BoxShape.circle,
                                ),
                              ),
                              const SizedBox(width: 6),
                              Text(
                                scaleReading.isOverload
                                    ? 'OVERLOAD'
                                    : scaleReading.isStable
                                        ? 'STABLE'
                                        : 'IN MOTION',
                                style: TextStyle(
                                  fontSize: 11,
                                  fontWeight: FontWeight.w800,
                                  letterSpacing: 0.5,
                                  color: scaleReading.isOverload
                                      ? Colors.red
                                      : scaleReading.isStable
                                          ? AppColors.accentEmerald
                                          : Colors.amber,
                                ),
                              ),
                            ],
                          ),
                        ),
                        if (scaleReading.isTare)
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                            decoration: BoxDecoration(
                              color: Colors.white12,
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: const Text(
                              'NET (TARE)',
                              style: TextStyle(
                                color: Colors.white70,
                                fontSize: 10,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: 12),

                    // Digits
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      crossAxisAlignment: CrossAxisAlignment.baseline,
                      textBaseline: TextBaseline.alphabetic,
                      children: [
                        Text(
                          activeWeight.toStringAsFixed(3),
                          style: GoogleFonts.jetBrainsMono(
                            fontSize: 48,
                            fontWeight: FontWeight.w800,
                            color: Colors.white,
                            letterSpacing: -1,
                          ),
                        ),
                        const SizedBox(width: 8),
                        Text(
                          scaleReading.unit,
                          style: GoogleFonts.outfit(
                            fontSize: 20,
                            fontWeight: FontWeight.w600,
                            color: Colors.white70,
                          ),
                        ),
                      ],
                    ),

                    const SizedBox(height: 8),
                    // Scale controls: Zero & Tare
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        OutlinedButton.icon(
                          style: OutlinedButton.styleFrom(
                            foregroundColor: Colors.white70,
                            side: const BorderSide(color: Colors.white24),
                            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                          ),
                          onPressed: () => scaleService.zero(),
                          icon: const Icon(Icons.replay_rounded, size: 16),
                          label: const Text('ZERO'),
                        ),
                        const SizedBox(width: 12),
                        OutlinedButton.icon(
                          style: OutlinedButton.styleFrom(
                            foregroundColor: Colors.white70,
                            side: const BorderSide(color: Colors.white24),
                            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                          ),
                          onPressed: () => scaleService.tare(),
                          icon: const Icon(Icons.exposure_rounded, size: 16),
                          label: const Text('TARE'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),

              const SizedBox(height: 20),

              // --- PRESET TINGI PORTIONS (if configured) ---
              if (widget.item.tingiAllowedSizes.isNotEmpty) ...[
                Text(
                  'Tingi Portions (Pre-packed)',
                  style: GoogleFonts.plusJakartaSans(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final size in widget.item.tingiAllowedSizes)
                      ActionChip(
                        label: Text(
                          size >= 1
                              ? '${size.toStringAsFixed(size.truncateToDouble() == size ? 0 : 2)} kg'
                              : '${(size * 1000).toInt()} g',
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                        backgroundColor: AppColors.cardHover,
                        side: const BorderSide(color: AppColors.border),
                        onPressed: () {
                          // Quick simulate / set portion
                          if (scaleService.driver is MockScaleDriver) {
                            (scaleService.driver as MockScaleDriver).setSimulatedWeight(size);
                          } else {
                            setState(() {
                              _isManualEntry = true;
                              _manualController.text = size.toString();
                            });
                          }
                        },
                      ),
                  ],
                ),
                const SizedBox(height: 16),
              ],

              // --- MANUAL WEIGHT ENTRY TOGGLE ---
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    'Manual Weight Override',
                    style: GoogleFonts.plusJakartaSans(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  Switch.adaptive(
                    value: _isManualEntry,
                    activeColor: AppColors.brandPrimary,
                    onChanged: (v) {
                      setState(() {
                        _isManualEntry = v;
                        if (v && _manualController.text.isEmpty) {
                          _manualController.text =
                              scaleReading.weight > 0 ? scaleReading.weight.toStringAsFixed(3) : '';
                        }
                      });
                    },
                  ),
                ],
              ),

              if (_isManualEntry) ...[
                const SizedBox(height: 8),
                TextField(
                  controller: _manualController,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  autofocus: true,
                  onChanged: (_) => setState(() {}),
                  decoration: InputDecoration(
                    labelText: 'Enter Weight (kg)',
                    suffixText: 'kg',
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ],

              const SizedBox(height: 20),

              // --- LINE TOTAL COMPUTATION ---
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: AppColors.background,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: AppColors.border),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Expanded(
                      child: Text(
                        'Computed Line Total',
                        style: GoogleFonts.plusJakartaSans(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      formatCurrency(lineTotal),
                      style: GoogleFonts.jetBrainsMono(
                        fontSize: 22,
                        fontWeight: FontWeight.w800,
                        color: AppColors.brandPrimary,
                      ),
                    ),
                  ],
                ),
              ),

              const SizedBox(height: 24),

              // --- ADD TO CART CTA ---
              SizedBox(
                height: 52,
                child: FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.brandPrimary,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  ),
                  onPressed: canSubmit ? () => _submitWeight(activeWeight) : null,
                  child: _isSubmitting
                      ? const SizedBox(
                          width: 24,
                          height: 24,
                          child: CircularProgressIndicator(
                            strokeWidth: 2.5,
                            color: Colors.white,
                          ),
                        )
                      : Text(
                          activeWeight > 0 ? 'Add ${activeWeight.toStringAsFixed(3)} kg to Cart' : 'Weigh Item',
                          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
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
