import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(title: Text('Departments: $branchName')),
      body: departmentsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load departments: $error')),
        data: (departments) {
          if (departments.isEmpty) {
            return const Center(
              child: Text('No departments yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () =>
                    ref
                        .read(departmentListProvider(branchId).notifier)
                        .refresh(),
            child: ListView.builder(
              itemCount: departments.length,
              itemBuilder: (context, index) {
                final department = departments[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.storefront)),
                  title: Text(department.name),
                  subtitle:
                      department.concessionaireContactInfo == null
                          ? null
                          : Text(department.concessionaireContactInfo!),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(
                builder:
                    (_) => AddDepartmentScreen(
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
