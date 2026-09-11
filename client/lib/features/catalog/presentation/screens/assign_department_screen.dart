import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../onboarding/domain/department_models.dart';
import '../../domain/item_models.dart';
import '../providers/catalog_providers.dart';

/// B6 — assigns (or clears) an item's department/concessionaire. Departments
/// themselves are managed per-branch in the onboarding feature; this screen
/// just picks one from across every branch.
class AssignDepartmentScreen extends ConsumerStatefulWidget {
  const AssignDepartmentScreen({super.key, required this.item});

  final Item item;

  @override
  ConsumerState<AssignDepartmentScreen> createState() =>
      _AssignDepartmentScreenState();
}

class _AssignDepartmentScreenState
    extends ConsumerState<AssignDepartmentScreen> {
  String? _selectedDepartmentId;

  @override
  void initState() {
    super.initState();
    _selectedDepartmentId = widget.item.departmentId;
  }

  Future<void> _submit() async {
    final controller = ref.read(
      updateItemDepartmentControllerProvider(widget.item.id).notifier,
    );
    final succeeded = await controller.updateDepartment(
      UpdateItemDepartmentRequest(departmentId: _selectedDepartmentId),
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
    final departmentsAsync = ref.watch(allDepartmentsProvider);
    final updateState = ref.watch(
      updateItemDepartmentControllerProvider(widget.item.id),
    );
    final isLoading = updateState.isLoading;
    final failure =
        ref
            .read(
              updateItemDepartmentControllerProvider(widget.item.id).notifier,
            )
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Assign Department: ${widget.item.name}')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: Padding(
              padding: const EdgeInsets.all(32),
              child: departmentsAsync.when(
                loading: () => const CircularProgressIndicator(),
                error:
                    (error, stackTrace) =>
                        Text('Could not load departments: $error'),
                data: (departments) {
                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      DropdownButtonFormField<String?>(
                        value: _selectedDepartmentId,
                        decoration: const InputDecoration(
                          labelText: 'Department (optional)',
                          border: OutlineInputBorder(),
                        ),
                        items: [
                          const DropdownMenuItem(
                            value: null,
                            child: Text('None'),
                          ),
                          for (final Department department in departments)
                            DropdownMenuItem(
                              value: department.id,
                              child: Text(department.name),
                            ),
                        ],
                        onChanged:
                            isLoading
                                ? null
                                : (value) => setState(
                                  () => _selectedDepartmentId = value,
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
                  );
                },
              ),
            ),
          ),
        ),
      ),
    );
  }
}
