import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/item_variant_models.dart';
import '../providers/catalog_providers.dart';

class AddVariantScreen extends ConsumerStatefulWidget {
  const AddVariantScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  ConsumerState<AddVariantScreen> createState() => _AddVariantScreenState();
}

class _AttributeRow {
  _AttributeRow()
    : keyController = TextEditingController(),
      valueController = TextEditingController();

  final TextEditingController keyController;
  final TextEditingController valueController;

  void dispose() {
    keyController.dispose();
    valueController.dispose();
  }
}

class _AddVariantScreenState extends ConsumerState<AddVariantScreen> {
  final _formKey = GlobalKey<FormState>();
  final _skuController = TextEditingController();
  final _priceOverrideController = TextEditingController();
  final List<_AttributeRow> _attributeRows = [_AttributeRow()];

  @override
  void dispose() {
    _skuController.dispose();
    _priceOverrideController.dispose();
    for (final row in _attributeRows) {
      row.dispose();
    }
    super.dispose();
  }

  void _addAttributeRow() {
    setState(() => _attributeRows.add(_AttributeRow()));
  }

  void _removeAttributeRow(int index) {
    setState(() {
      _attributeRows.removeAt(index).dispose();
    });
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final attributes = <String, String>{
      for (final row in _attributeRows)
        if (row.keyController.text.trim().isNotEmpty)
          row.keyController.text.trim(): row.valueController.text.trim(),
    };

    if (attributes.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Add at least one attribute.')),
      );
      return;
    }

    final controller = ref.read(
      createVariantControllerProvider(widget.itemId).notifier,
    );
    final succeeded = await controller.create(
      CreateItemVariantRequest(
        attributes: attributes,
        sku:
            _skuController.text.trim().isEmpty
                ? null
                : _skuController.text.trim(),
        priceOverride:
            _priceOverrideController.text.trim().isEmpty
                ? null
                : double.parse(_priceOverrideController.text.trim()),
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
      createVariantControllerProvider(widget.itemId),
    );
    final isLoading = createState.isLoading;
    final failure =
        ref
            .read(createVariantControllerProvider(widget.itemId).notifier)
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Add Variant: ${widget.itemName}')),
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
                    Text(
                      'Attributes',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 8),
                    for (
                      var index = 0;
                      index < _attributeRows.length;
                      index++
                    ) ...[
                      Row(
                        children: [
                          Expanded(
                            child: TextFormField(
                              controller: _attributeRows[index].keyController,
                              enabled: !isLoading,
                              decoration: const InputDecoration(
                                labelText: 'Attribute (e.g. Size)',
                                border: OutlineInputBorder(),
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: TextFormField(
                              controller: _attributeRows[index].valueController,
                              enabled: !isLoading,
                              decoration: const InputDecoration(
                                labelText: 'Value (e.g. Large)',
                                border: OutlineInputBorder(),
                              ),
                            ),
                          ),
                          IconButton(
                            onPressed:
                                isLoading || _attributeRows.length == 1
                                    ? null
                                    : () => _removeAttributeRow(index),
                            icon: const Icon(Icons.remove_circle_outline),
                            tooltip: 'Remove attribute',
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                    ],
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton.icon(
                        onPressed: isLoading ? null : _addAttributeRow,
                        icon: const Icon(Icons.add),
                        label: const Text('Add attribute'),
                      ),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _skuController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'SKU (optional)',
                        border: OutlineInputBorder(),
                      ),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _priceOverrideController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Price override (optional)',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      validator: (value) {
                        if (value == null || value.trim().isEmpty) {
                          return null;
                        }
                        final parsed = double.tryParse(value.trim());
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
                                : const Text('Add Variant'),
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
