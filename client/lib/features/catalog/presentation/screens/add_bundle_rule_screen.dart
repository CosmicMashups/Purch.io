import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/bundle_promo_rule_models.dart';
import '../providers/catalog_providers.dart';

class AddBundleRuleScreen extends ConsumerStatefulWidget {
  const AddBundleRuleScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  ConsumerState<AddBundleRuleScreen> createState() =>
      _AddBundleRuleScreenState();
}

class _AddBundleRuleScreenState extends ConsumerState<AddBundleRuleScreen> {
  final _formKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();
  final _triggerQuantityController = TextEditingController();
  final _bundlePriceController = TextEditingController();

  @override
  void dispose() {
    _descriptionController.dispose();
    _triggerQuantityController.dispose();
    _bundlePriceController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(
      createBundleRuleControllerProvider(widget.itemId).notifier,
    );
    final succeeded = await controller.create(
      CreateBundlePromoRuleRequest(
        description: _descriptionController.text.trim(),
        triggerQuantity: int.parse(_triggerQuantityController.text.trim()),
        bundlePrice: double.parse(_bundlePriceController.text.trim()),
      ),
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
    final createState = ref.watch(
      createBundleRuleControllerProvider(widget.itemId),
    );
    final isLoading = createState.isLoading;
    final failure =
        ref
            .read(createBundleRuleControllerProvider(widget.itemId).notifier)
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Add Bundle Rule: ${widget.itemName}')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(32),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextFormField(
                      controller: _descriptionController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Description',
                        border: OutlineInputBorder(),
                      ),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _triggerQuantityController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Buy quantity',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: TextInputType.number,
                      validator: (value) {
                        final parsed = int.tryParse(value?.trim() ?? '');
                        if (parsed == null || parsed < 2) {
                          return 'Enter a quantity of 2 or more';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _bundlePriceController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Bundle price',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      validator: (value) {
                        final parsed = double.tryParse(value?.trim() ?? '');
                        if (parsed == null || parsed < 0) {
                          return 'Enter a valid price';
                        }
                        return null;
                      },
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
                                : const Text('Add Bundle Rule'),
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
