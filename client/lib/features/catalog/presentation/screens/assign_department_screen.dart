import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
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
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Assign Department: ${widget.item.name}'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.xl),
              child: Card(
                elevation: 0,
                color: AppColors.surface,
                shape: const RoundedRectangleBorder(
                  borderRadius: AppRadius.lgBorder,
                  side: BorderSide(color: AppColors.border),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.xl),
                  child: departmentsAsync.when(
                    loading: () => const Center(
                      child: Padding(
                        padding: EdgeInsets.all(AppSpacing.xl),
                        child: CircularProgressIndicator(),
                      ),
                    ),
                    error:
                        (error, stackTrace) =>
                            Text('Could not load departments: $error', style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.error)),
                    data: (departments) {
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Text(
                            'Department Assignment',
                            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                              fontWeight: FontWeight.w700,
                              color: AppColors.textPrimary,
                            ),
                          ),
                          const SizedBox(height: AppSpacing.xs),
                          Text(
                            'Assign this item to a concessionaire department for isolated revenue tracking.',
                            style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
                          ),
                          const SizedBox(height: AppSpacing.lg),
                          DropdownButtonFormField<String?>(
                            value: _selectedDepartmentId,
                            decoration: const InputDecoration(
                              labelText: 'Department (optional)',
                              border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
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
                            const SizedBox(height: AppSpacing.md),
                            Container(
                              padding: const EdgeInsets.all(AppSpacing.md),
                              decoration: BoxDecoration(
                                color: AppColors.error.withValues(alpha: 0.08),
                                borderRadius: AppRadius.smBorder,
                                border: Border.all(color: AppColors.error.withValues(alpha: 0.2)),
                              ),
                              child: Text(
                                failure.message,
                                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                                  color: AppColors.error,
                                ),
                                textAlign: TextAlign.center,
                              ),
                            ),
                          ],
                          const SizedBox(height: AppSpacing.xl),
                          SizedBox(
                            height: 52,
                            child: FilledButton(
                              onPressed: isLoading ? null : _submit,
                              style: FilledButton.styleFrom(
                                backgroundColor: AppColors.brandPrimary,
                                shape: const RoundedRectangleBorder(borderRadius: AppRadius.smBorder),
                              ),
                              child:
                                  isLoading
                                      ? const SizedBox(
                                        height: 20,
                                        width: 20,
                                        child: CircularProgressIndicator(
                                          strokeWidth: 2,
                                          color: Colors.white,
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
        ),
      ),
    );
  }
}
