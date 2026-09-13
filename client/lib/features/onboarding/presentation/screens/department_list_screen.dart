import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/onboarding_providers.dart';
import 'add_department_screen.dart';

/// B6 — departments/concessionaires within a branch (e.g. a "Bakery Stall"
/// with its own concessionaire contact). Items are assigned to a department
/// from the catalog feature (see Item.departmentId), not here.
class DepartmentListScreen extends ConsumerWidget {
  const DepartmentListScreen({
    super.key,
    required this.branchId,
    required this.branchName,
  });

  final String branchId;
  final String branchName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final departmentsAsync = ref.watch(departmentListProvider(branchId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Departments: $branchName'),
        elevation: 0,
      ),
      body: departmentsAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: error.toString(),
          onRetry: () =>
              ref.read(departmentListProvider(branchId).notifier).refresh(),
        ),
        data: (departments) {
          if (departments.isEmpty) {
            return EmptyStateView(
              icon: Icons.storefront_outlined,
              title: 'No departments yet — tap + to add one.',
              description:
                  'Create departments or concessionaire stalls within this branch to organize inventory and sales reporting.',
              actionLabel: 'Add Department',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => AddDepartmentScreen(
                    branchId: branchId,
                    branchName: branchName,
                  ),
                ),
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh: () =>
                ref.read(departmentListProvider(branchId).notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.md),
              itemCount: departments.length,
              separatorBuilder: (context, index) =>
                  const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final department = departments[index];
                return Container(
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.mdBorder,
                    border: Border.all(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.xs,
                    ),
                    leading: Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        borderRadius: AppRadius.smBorder,
                      ),
                      child: const Icon(
                        Icons.storefront_rounded,
                        color: AppColors.brandPrimary,
                      ),
                    ),
                    title: Text(
                      department.name,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: department.concessionaireContactInfo != null
                        ? Text(
                            department.concessionaireContactInfo!,
                            style: const TextStyle(
                              fontSize: 13,
                              color: AppColors.textSecondary,
                            ),
                          )
                        : null,
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
        onPressed: () => Navigator.of(context).push<void>(
          MaterialPageRoute(
            builder: (_) => AddDepartmentScreen(
              branchId: branchId,
              branchName: branchName,
            ),
          ),
        ),
        tooltip: 'Add department',
        child: const Icon(Icons.add),
      ),
    );
  }
}

