import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/modifier_models.dart';
import '../providers/catalog_providers.dart';

class AddModifierScreen extends ConsumerStatefulWidget {
  const AddModifierScreen({
    super.key,
    required this.groupId,
    required this.groupName,
  });

  final String groupId;
  final String groupName;

  @override
  ConsumerState<AddModifierScreen> createState() => _AddModifierScreenState();
}

class _AddModifierScreenState extends ConsumerState<AddModifierScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _priceDeltaController = TextEditingController(text: '0');

  @override
  void dispose() {
    _nameController.dispose();
    _priceDeltaController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(
      addModifierControllerProvider(widget.groupId).notifier,
    );
    final succeeded = await controller.add(
      CreateItemModifierRequest(
        name: _nameController.text.trim(),
        priceDelta: double.parse(_priceDeltaController.text.trim()),
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
    final addState = ref.watch(addModifierControllerProvider(widget.groupId));
    final isLoading = addState.isLoading;
    final failure =
        ref
            .read(addModifierControllerProvider(widget.groupId).notifier)
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Add Option to ${widget.groupName}')),
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
                      controller: _nameController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Option name (e.g. "Extra Cheese")',
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
                      controller: _priceDeltaController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Price add-on',
                        border: OutlineInputBorder(),
                        prefixText: '₱ ',
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      validator:
                          (value) =>
                              double.tryParse(value?.trim() ?? '') == null
                                  ? 'Enter a valid amount'
                                  : null,
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
                                : const Text('Add Option'),
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
