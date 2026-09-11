import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
            constraints: const BoxConstraints(maxWidth: 420),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(32),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  TextField(
                    controller: _thresholdController,
                    enabled: !isLoading,
                    decoration: const InputDecoration(
                      labelText: 'Alert when stock falls to (blank = off)',
                      border: OutlineInputBorder(),
                    ),
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                  ),
                  if (failure != null) ...[
                    const SizedBox(height: 16),
                    Text(
                      failure.message,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                      textAlign: TextAlign.center,
                    ),
                  ],
                  const SizedBox(height: 24),
                  SizedBox(
                    height: 56,
                    child: FilledButton(
                      onPressed: isLoading ? null : _submit,
                      child:
                          isLoading
                              ? const SizedBox(
                                height: 24,
                                width: 24,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2.5,
                                ),
                              )
                              : const Text('Save'),
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
