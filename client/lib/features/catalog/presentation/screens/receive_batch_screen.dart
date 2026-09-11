import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/item_batch_models.dart';
import '../providers/catalog_providers.dart';

class ReceiveBatchScreen extends ConsumerStatefulWidget {
  const ReceiveBatchScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  ConsumerState<ReceiveBatchScreen> createState() => _ReceiveBatchScreenState();
}

class _ReceiveBatchScreenState extends ConsumerState<ReceiveBatchScreen> {
  final _formKey = GlobalKey<FormState>();
  final _lotNumberController = TextEditingController();
  final _quantityController = TextEditingController();
  DateTime? _expiryDate;

  @override
  void dispose() {
    _lotNumberController.dispose();
    _quantityController.dispose();
    super.dispose();
  }

  Future<void> _pickExpiryDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime.now(),
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 365 * 5)),
    );
    if (picked != null) {
      setState(() => _expiryDate = picked);
    }
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(
      receiveBatchControllerProvider(widget.itemId).notifier,
    );
    final succeeded = await controller.receive(
      CreateItemBatchRequest(
        lotNumber: _lotNumberController.text.trim(),
        expiryDate: _expiryDate,
        quantityReceived: double.parse(_quantityController.text.trim()),
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
    final receiveState = ref.watch(
      receiveBatchControllerProvider(widget.itemId),
    );
    final isLoading = receiveState.isLoading;
    final failure =
        ref
            .read(receiveBatchControllerProvider(widget.itemId).notifier)
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Receive Stock: ${widget.itemName}')),
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
                      controller: _lotNumberController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Lot number',
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
                      controller: _quantityController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Quantity received',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      validator: (value) {
                        final parsed = double.tryParse(value?.trim() ?? '');
                        if (parsed == null || parsed <= 0) {
                          return 'Enter a quantity greater than zero';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 16),
                    OutlinedButton(
                      onPressed: isLoading ? null : _pickExpiryDate,
                      child: Text(
                        _expiryDate == null
                            ? 'Set expiry date (optional)'
                            : 'Expires: ${_expiryDate!.toLocal().toString().split(' ').first}',
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
                                : const Text('Receive Stock'),
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
