import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/branch_models.dart';
import '../../domain/device_models.dart';
import '../providers/onboarding_providers.dart';

class AddDeviceScreen extends ConsumerStatefulWidget {
  const AddDeviceScreen({super.key});

  @override
  ConsumerState<AddDeviceScreen> createState() => _AddDeviceScreenState();
}

class _AddDeviceScreenState extends ConsumerState<AddDeviceScreen> {
  final _formKey = GlobalKey<FormState>();
  final _identifierController = TextEditingController();
  Branch? _selectedBranch;

  @override
  void dispose() {
    _identifierController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false) ||
        _selectedBranch == null) {
      return;
    }

    final controller = ref.read(createDeviceControllerProvider.notifier);
    final succeeded = await controller.create(
      CreateDeviceRequest(
        branchId: _selectedBranch!.id,
        deviceIdentifier:
            _identifierController.text.trim().isEmpty
                ? null
                : _identifierController.text.trim(),
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
    final branchesAsync = ref.watch(branchListProvider);
    final createState = ref.watch(createDeviceControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createDeviceControllerProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(title: const Text('Pair a Device')),
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
                    branchesAsync.when(
                      loading:
                          () =>
                              const Center(child: CircularProgressIndicator()),
                      error:
                          (error, stackTrace) =>
                              Text('Could not load branches: $error'),
                      data: (branches) {
                        _selectedBranch ??=
                            branches.isNotEmpty ? branches.first : null;
                        return DropdownButtonFormField<Branch>(
                          value: _selectedBranch,
                          decoration: const InputDecoration(
                            labelText: 'Branch',
                            border: OutlineInputBorder(),
                          ),
                          items:
                              branches
                                  .map(
                                    (branch) => DropdownMenuItem(
                                      value: branch,
                                      child: Text(branch.name),
                                    ),
                                  )
                                  .toList(),
                          onChanged:
                              isLoading
                                  ? null
                                  : (value) =>
                                      setState(() => _selectedBranch = value),
                          validator:
                              (value) => value == null ? 'Required' : null,
                        );
                      },
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _identifierController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Device label (optional, e.g. "Tablet 2")',
                        border: OutlineInputBorder(),
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
                                : const Text('Pair Device'),
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
