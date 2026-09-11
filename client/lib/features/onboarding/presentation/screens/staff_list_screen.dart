import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/onboarding_enums.dart';
import '../providers/onboarding_providers.dart';
import 'add_staff_screen.dart';

/// A4's staff list — one entry per staff member, role and active status
/// visible at a glance, per the design brief's "icon-forward, minimal text"
/// direction. Deactivating/reactivating (not deleting) matches Purch.LoginService's
/// active-users-only PIN check without needing a separate delete concept.
class StaffListScreen extends ConsumerWidget {
  const StaffListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final staffAsync = ref.watch(staffListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Staff')),
      body: staffAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load staff: $error')),
        data: (staff) {
          if (staff.isEmpty) {
            return const Center(
              child: Text('No staff yet — tap + to add your first one.'),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(staffListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: staff.length,
              itemBuilder: (context, index) {
                final member = staff[index];
                return ListTile(
                  leading: CircleAvatar(
                    child: Icon(
                      member.isActive ? Icons.person : Icons.person_off,
                    ),
                  ),
                  title: Text(member.name),
                  subtitle: Text(_roleLabel(member.role)),
                  trailing:
                      member.isActive
                          ? null
                          : const Chip(
                            label: Text('Inactive'),
                            visualDensity: VisualDensity.compact,
                          ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const AddStaffScreen()),
            ),
        tooltip: 'Add staff',
        child: const Icon(Icons.add),
      ),
    );
  }

  String _roleLabel(StaffRole role) => switch (role) {
    StaffRole.admin => 'Admin',
    StaffRole.manager => 'Manager',
    StaffRole.cashier => 'Cashier',
    StaffRole.warehouse => 'Warehouse',
  };
}
