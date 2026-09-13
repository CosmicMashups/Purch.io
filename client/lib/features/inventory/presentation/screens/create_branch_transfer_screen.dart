import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../onboarding/domain/branch_models.dart';
import '../../../onboarding/presentation/providers/onboarding_providers.dart';
import '../../domain/branch_transfer_models.dart';
import '../providers/branch_transfer_providers.dart';
import '../../../../core/errors/failure.dart';

class CreateBranchTransferScreen extends ConsumerStatefulWidget {
  const CreateBranchTransferScreen({super.key});

  @override
  ConsumerState<CreateBranchTransferScreen> createState() =>
      _CreateBranchTransferScreenState();
}

class _CreateBranchTransferScreenState
    extends ConsumerState<CreateBranchTransferScreen> {
  Branch? _sourceBranch;
  Branch? _destinationBranch;
  final List<_LineDraft> _lines = [_LineDraft()];

  @override
  void dispose() {
    for (final line in _lines) {
      line.quantityController.dispose();
    }
    super.dispose();
  }

  bool get _canSubmit {
    if (_sourceBranch == null ||
        _destinationBranch == null ||
        _sourceBranch!.id == _destinationBranch!.id) {
      return false;
    }
    for (final line in _lines) {
      final quantity = double.tryParse(line.quantityController.text.trim());
      if (line.item == null || quantity == null || quantity <= 0) {
        return false;
      }
    }
    return true;
  }

  Future<void> _submit() async {
    if (!_canSubmit) {
      return;
    }

    final controller = ref.read(
      createBranchTransferControllerProvider.notifier,
    );
    final succeeded = await controller.create(
      CreateBranchTransferRequest(
        sourceBranchId: _sourceBranch!.id,
        destinationBranchId: _destinationBranch!.id,
        lines: [
          for (final line in _lines)
            CreateBranchTransferLineRequest(
              itemId: line.item!.id,
              quantity: double.parse(line.quantityController.text.trim()),
            ),
        ],
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
    final itemsAsync = ref.watch(itemListProvider);
    final createState = ref.watch(createBranchTransferControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref
            .read(createBranchTransferControllerProvider.notifier)
            .currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('New Stock Transfer'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 540),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              child: Card(
                elevation: 0,
                color: AppColors.surface,
                shape: const RoundedRectangleBorder(
                  borderRadius: AppRadius.lgBorder,
                  side: BorderSide(color: AppColors.border),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.xl),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        'Transfer Details',
                        style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: AppColors.textPrimary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      branchesAsync.when(
                        loading: () => const LinearProgressIndicator(),
                        error:
                            (error, stackTrace) =>
                                Text('Could not load branches: ${describeError(error)}', style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.error)),
                        data:
                            (branches) => DropdownButtonFormField<Branch>(
                              value: _matchBranch(branches, _sourceBranch),
                              decoration: const InputDecoration(
                                labelText: 'Source branch',
                                border: OutlineInputBorder(
                                  borderRadius: AppRadius.smBorder,
                                ),
                              ),
                              items: [
                                for (final branch in branches)
                                  DropdownMenuItem(
                                    value: branch,
                                    child: Text(branch.name),
                                  ),
                              ],
                              onChanged:
                                  isLoading
                                      ? null
                                      : (branch) =>
                                          setState(() => _sourceBranch = branch),
                            ),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      branchesAsync.when(
                        loading: () => const LinearProgressIndicator(),
                        error:
                            (error, stackTrace) =>
                                Text('Could not load branches: ${describeError(error)}', style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.error)),
                        data:
                            (branches) => DropdownButtonFormField<Branch>(
                              value: _matchBranch(branches, _destinationBranch),
                              decoration: const InputDecoration(
                                labelText: 'Destination branch',
                                border: OutlineInputBorder(
                                  borderRadius: AppRadius.smBorder,
                                ),
                              ),
                              items: [
                                for (final branch in branches)
                                  DropdownMenuItem(
                                    value: branch,
                                    child: Text(branch.name),
                                  ),
                              ],
                              onChanged:
                                  isLoading
                                      ? null
                                      : (branch) => setState(
                                        () => _destinationBranch = branch,
                                      ),
                            ),
                      ),
                      const SizedBox(height: AppSpacing.lg),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text('Items', style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w600)),
                          TextButton.icon(
                            onPressed:
                                isLoading
                                    ? null
                                    : () => setState(
                                      () => _lines.add(_LineDraft()),
                                    ),
                            icon: const Icon(Icons.add, size: 18),
                            label: const Text('Add Item'),
                            style: TextButton.styleFrom(
                              foregroundColor: AppColors.brandPrimary,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.xs),
                      itemsAsync.when(
                        loading: () => const LinearProgressIndicator(),
                        error:
                            (error, stackTrace) =>
                                Text('Could not load items: ${describeError(error)}', style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.error)),
                        data:
                            (items) => Column(
                              children: [
                                for (var i = 0; i < _lines.length; i++)
                                  _LineRow(
                                    key: ValueKey(_lines[i]),
                                    items: items,
                                    draft: _lines[i],
                                    isLoading: isLoading,
                                    onChanged: () => setState(() {}),
                                    onRemove:
                                        _lines.length > 1
                                            ? () =>
                                                setState(() => _lines.removeAt(i))
                                            : null,
                                  ),
                              ],
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
                      const SizedBox(height: AppSpacing.lg),
                      SizedBox(
                        height: 52,
                        child: FilledButton(
                          onPressed: (!isLoading && _canSubmit) ? _submit : null,
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            shape: const RoundedRectangleBorder(
                              borderRadius: AppRadius.smBorder,
                            ),
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
                                  : const Text('Create Transfer'),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Branch? _matchBranch(List<Branch> branches, Branch? current) {
    if (current == null) {
      return null;
    }
    for (final branch in branches) {
      if (branch.id == current.id) {
        return branch;
      }
    }
    return null;
  }
}

class _LineDraft {
  Item? item;
  final quantityController = TextEditingController();
}

class _LineRow extends StatelessWidget {
  const _LineRow({
    super.key,
    required this.items,
    required this.draft,
    required this.isLoading,
    required this.onChanged,
    required this.onRemove,
  });

  final List<Item> items;
  final _LineDraft draft;
  final bool isLoading;
  final VoidCallback onChanged;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    Item? matchedItem;
    for (final item in items) {
      if (item.id == draft.item?.id) {
        matchedItem = item;
        break;
      }
    }

    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            flex: 2,
            child: DropdownButtonFormField<Item>(
              value: matchedItem,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'Item',
                border: OutlineInputBorder(
                  borderRadius: AppRadius.smBorder,
                ),
                isDense: true,
              ),
              items: [
                for (final item in items)
                  DropdownMenuItem(
                    value: item,
                    child: Text(item.name, overflow: TextOverflow.ellipsis),
                  ),
              ],
              onChanged:
                  isLoading
                      ? null
                      : (item) {
                        draft.item = item;
                        onChanged();
                      },
            ),
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: TextField(
              controller: draft.quantityController,
              enabled: !isLoading,
              decoration: const InputDecoration(
                labelText: 'Qty',
                border: OutlineInputBorder(
                  borderRadius: AppRadius.smBorder,
                ),
                isDense: true,
              ),
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              onChanged: (_) => onChanged(),
            ),
          ),
          if (onRemove != null)
            IconButton(
              onPressed: isLoading ? null : onRemove,
              icon: const Icon(Icons.remove_circle_outline, color: AppColors.textMuted),
              tooltip: 'Remove item',
            ),
        ],
      ),
    );
  }
}
