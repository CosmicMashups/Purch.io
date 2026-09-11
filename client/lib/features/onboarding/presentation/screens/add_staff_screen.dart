import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/onboarding_enums.dart';
import '../../domain/staff_models.dart';
import '../providers/onboarding_providers.dart';

/// Creates a tenant-wide staff account (A4). Branch-scoped staff creation
/// (assigning a specific branch/ScopeType.branch) is added once the branch
/// management screen exists to pick a branch from.
class AddStaffScreen extends ConsumerStatefulWidget {
  const AddStaffScreen({super.key});

  @override
  ConsumerState<AddStaffScreen> createState() => _AddStaffScreenState();
}

class _AddStaffScreenState extends ConsumerState<AddStaffScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _pinController = TextEditingController();
  StaffRole _role = StaffRole.cashier;

  @override
  void dispose() {
    _nameController.dispose();
    _pinController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(createStaffControllerProvider.notifier);
    final succeeded = await controller.create(
      CreateStaffRequest(
        name: _nameController.text.trim(),
        role: _role,
        pin: _pinController.text.trim(),
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
    final createState = ref.watch(createStaffControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createStaffControllerProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(title: const Text('Add Staff')),
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
                        labelText: 'Full name',
                        border: OutlineInputBorder(),
                      ),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    const SizedBox(height: 16),
                    DropdownButtonFormField<StaffRole>(
                      value: _role,
                      decoration: const InputDecoration(
                        labelText: 'Role',
                        border: OutlineInputBorder(),
                      ),
                      items:
                          StaffRole.values
                              .map(
                                (role) => DropdownMenuItem(
                                  value: role,
                                  child: Text(_label(role)),
                                ),
                              )
                              .toList(),
                      onChanged:
                          isLoading
                              ? null
                              : (value) =>
                                  setState(() => _role = value ?? _role),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _pinController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'PIN',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: TextInputType.number,
                      obscureText: true,
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
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
                                : const Text('Add Staff Member'),
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

  String _label(StaffRole role) => switch (role) {
    StaffRole.admin => 'Admin',
    StaffRole.manager => 'Manager',
    StaffRole.cashier => 'Cashier',
    StaffRole.warehouse => 'Warehouse',
  };
}
