import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/item_models.dart';
import '../providers/catalog_providers.dart';

/// C1 — StockOnHand at or below this (and above zero) triggers the
/// inventory dashboard's low-stock alert. Leaving the field blank clears
/// the alert for this item.
class LowStockThresholdScreen extends ConsumerStatefulWidget {
  const LowStockThresholdScreen({super.key, required this.item});

  final Item item;

  @override
  ConsumerState<LowStockThresholdScreen> createState() =>
      _LowStockThresholdScreenState();
}

class _LowStockThresholdScreenState
    extends ConsumerState<LowStockThresholdScreen> {
  late final TextEditingController _thresholdController;

  @override
  void initState() {
    super.initState();
    _thresholdController = TextEditingController(
      text: widget.item.lowStockThreshold?.toString() ?? '',
    );
  }

  @override
  void dispose() {
    _thresholdController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final text = _thresholdController.text.trim();
    final threshold = text.isEmpty ? null : double.tryParse(text);
    if (text.isNotEmpty && threshold == null) {
      return;
    }

    final controller = ref.read(
      updateLowStockThresholdControllerProvider(widget.item.id).notifier,
    );
    final succeeded = await controller.updateThreshold(
      UpdateLowStockThresholdRequest(threshold: threshold),
    );

    if (!mounted) {
      return;
    }

    if (succeeded) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final updateState = ref.watch(
      updateLowStockThresholdControllerProvider(widget.item.id),
    );
    final isLoading = updateState.isLoading;
    final failure =
        ref
            .read(
              updateLowStockThresholdControllerProvider(
                widget.item.id,
              ).notifier,
            )
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Low-Stock Threshold: ${widget.item.name}')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 460),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.lg,
                vertical: AppSpacing.md,
              ),
              child: Container(
                padding: const EdgeInsets.all(AppSpacing.lg),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: AppRadius.lgBorder,
                  border: Border.all(color: AppColors.border),
                  boxShadow: AppShadows.subtle,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextField(
                      controller: _thresholdController,
                      enabled: !isLoading,
                      decoration: InputDecoration(
                        labelText: 'Alert when stock falls to (blank = off)',
                        labelStyle: const TextStyle(color: AppColors.textSecondary),
                        prefixIcon: const Icon(Icons.warning_amber_rounded, color: AppColors.accentWarm),
                        filled: true,
                        fillColor: AppColors.background,
                        border: OutlineInputBorder(
                          borderRadius: AppRadius.mdBorder,
                          borderSide: const BorderSide(color: AppColors.border),
                        ),
                        enabledBorder: OutlineInputBorder(
                          borderRadius: AppRadius.mdBorder,
                          borderSide: const BorderSide(color: AppColors.border),
                        ),
                        focusedBorder: OutlineInputBorder(
                          borderRadius: AppRadius.mdBorder,
                          borderSide: const BorderSide(color: AppColors.brandPrimary, width: 2),
                        ),
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                    ),
                    if (failure != null) ...[
                      const SizedBox(height: AppSpacing.md),
                      Container(
                        padding: const EdgeInsets.all(AppSpacing.sm),
                        decoration: BoxDecoration(
                          color: AppColors.cardHover,
                          borderRadius: AppRadius.mdBorder,
                          border: Border.all(color: AppColors.error),
                        ),
                        child: Text(
                          failure.message,
                          style: const TextStyle(
                            color: AppColors.error,
                            fontWeight: FontWeight.w500,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ),
                    ],
                    const SizedBox(height: AppSpacing.lg),
                    SizedBox(
                      height: 52,
                      child: FilledButton(
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColors.brandPrimary,
                          foregroundColor: AppColors.onBrandPrimary,
                          shape: const RoundedRectangleBorder(
                            borderRadius: AppRadius.mdBorder,
                          ),
                        ),
                        onPressed: isLoading ? null : _submit,
                        child:
                            isLoading
                                ? const SizedBox(
                                  height: 24,
                                  width: 24,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2.5,
                                    color: AppColors.onBrandPrimary,
                                  ),
                                )
                                : const Text(
                                  'Save',
                                  style: TextStyle(
                                    fontSize: 16,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
