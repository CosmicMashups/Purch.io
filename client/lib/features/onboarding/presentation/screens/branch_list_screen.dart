import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/onboarding_providers.dart';
import 'add_branch_screen.dart';

/// A3's branch list — multi-branch from the start (not single-enforced), per
/// the implementation plan.
class BranchListScreen extends ConsumerWidget {
  const BranchListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final branchesAsync = ref.watch(branchListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Branches')),
      body: branchesAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load branches: $error')),
        data: (branches) {
          if (branches.isEmpty) {
            return const Center(
              child: Text('No branches yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(branchListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: branches.length,
              itemBuilder: (context, index) {
                final branch = branches[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.store)),
                  title: Text(branch.name),
                  subtitle:
                      branch.address == null ? null : Text(branch.address!),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const AddBranchScreen()),
            ),
        tooltip: 'Add branch',
        child: const Icon(Icons.add),
      ),
    );
  }
}
