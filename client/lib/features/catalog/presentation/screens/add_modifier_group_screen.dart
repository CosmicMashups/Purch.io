import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/modifier_models.dart';
import '../providers/catalog_providers.dart';

class AddModifierGroupScreen extends ConsumerStatefulWidget {
  const AddModifierGroupScreen({super.key});

  @override
  ConsumerState<AddModifierGroupScreen> createState() =>
      _AddModifierGroupScreenState();
}

class _AddModifierGroupScreenState
    extends ConsumerState<AddModifierGroupScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  bool _allowMultipleSelection = false;

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(createModifierGroupControllerProvider.notifier);
    final succeeded = await controller.create(
      CreateModifierGroupRequest(
        name: _nameController.text.trim(),
        allowMultipleSelection: _allowMultipleSelection,
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
    final createState = ref.watch(createModifierGroupControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createModifierGroupControllerProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(title: const Text('Add Modifier Group')),
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
                        labelText: 'Group name (e.g. "Add-ons")',
                        border: OutlineInputBorder(),
                      ),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    SwitchListTile(
                      title: const Text('Allow selecting more than one'),
                      value: _allowMultipleSelection,
                      onChanged:
                          isLoading
                              ? null
                              : (value) => setState(
                                () => _allowMultipleSelection = value,
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
                                : const Text('Add Group'),
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
