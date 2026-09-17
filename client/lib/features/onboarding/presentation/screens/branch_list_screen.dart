import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/onboarding_providers.dart';
import 'add_branch_screen.dart';
import 'department_list_screen.dart';
import 'hardware_settings_screen.dart';
import 'manual_gcash_qr_settings_screen.dart';

enum _BranchAction { departments, manualGcashQr, hardwareSettings }

/// A3's branch list — multi-branch from the start (not single-enforced), per
/// the implementation plan. Each branch offers its departments (B6) and its
/// manual GCash QR payment settings (D5) via a per-row menu.
class BranchListScreen extends ConsumerWidget {
  const BranchListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final branchesAsync = ref.watch(branchListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Branches'),
        elevation: 0,
      ),
      body: branchesAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: error.toString(),
          onRetry: () => ref.read(branchListProvider.notifier).refresh(),
        ),
        data: (branches) {
          if (branches.isEmpty) {
            return EmptyStateView(
              icon: Icons.store_outlined,
              title: 'No branches yet — tap + to add one.',
              description:
                  'Set up your physical stores or pop-up branches to manage departmental inventory, terminals, and staff.',
              actionLabel: 'Add Branch',
              onAction:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const AddBranchScreen()),
                  ),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh: () => ref.read(branchListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.md),
              itemCount: branches.length,
              separatorBuilder: (context, index) =>
                  const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final branch = branches[index];
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
                      branch.name,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: branch.address != null
                        ? Text(
                            branch.address!,
                            style: const TextStyle(
                              fontSize: 13,
                              color: AppColors.textSecondary,
                            ),
                          )
                        : null,
                    trailing: PopupMenuButton<_BranchAction>(
                      tooltip: 'Branch actions',
                      icon: const Icon(
                        Icons.more_vert_rounded,
                        color: AppColors.textSecondary,
                      ),
                      shape: RoundedRectangleBorder(
                        borderRadius: AppRadius.smBorder,
                      ),
                      onSelected: (action) {
                        switch (action) {
                          case _BranchAction.departments:
                            Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder: (_) => DepartmentListScreen(
                                  branchId: branch.id,
                                  branchName: branch.name,
                                ),
                              ),
                            );
                            break;
                          case _BranchAction.manualGcashQr:
                            Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder: (_) => ManualGcashQrSettingsScreen(
                                  branch: branch,
                                ),
                              ),
                            );
                            break;
                          case _BranchAction.hardwareSettings:
                            Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder: (_) => HardwareSettingsScreen(
                                  branchId: branch.id,
                                ),
                              ),
                            );
                            break;
                        }
                      },
                      itemBuilder: (context) => const [
                        PopupMenuItem(
                          value: _BranchAction.departments,
                          child: Row(
                            children: [
                              Icon(
                                Icons.category_outlined,
                                size: 18,
                                color: AppColors.textSecondary,
                              ),
                              SizedBox(width: AppSpacing.sm),
                              Flexible(
                                child: Text(
                                  'Departments',
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                            ],
                          ),
                        ),
                        PopupMenuItem(
                          value: _BranchAction.manualGcashQr,
                          child: Row(
                            children: [
                              Icon(
                                Icons.qr_code_2_rounded,
                                size: 18,
                                color: AppColors.textSecondary,
                              ),
                              SizedBox(width: AppSpacing.sm),
                              Flexible(
                                child: Text(
                                  'Manual GCash QR',
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                            ],
                          ),
                        ),
                        PopupMenuItem(
                          value: _BranchAction.hardwareSettings,
                          child: Row(
                            children: [
                              Icon(
                                Icons.print_rounded,
                                size: 18,
                                color: AppColors.textSecondary,
                              ),
                              SizedBox(width: AppSpacing.sm),
                              Flexible(
                                child: Text(
                                  'Hardware Settings',
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
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
          MaterialPageRoute(builder: (_) => const AddBranchScreen()),
        ),
        tooltip: 'Add branch',
        child: const Icon(Icons.add),
      ),
    );
  }
}

