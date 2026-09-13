import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/modifier_models.dart';
import '../providers/catalog_providers.dart';
import '../../../../core/errors/failure.dart';

class AttachModifierGroupScreen extends ConsumerStatefulWidget {
  const AttachModifierGroupScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  ConsumerState<AttachModifierGroupScreen> createState() =>
      _AttachModifierGroupScreenState();
}

class _AttachModifierGroupScreenState
    extends ConsumerState<AttachModifierGroupScreen> {
  String? _selectedGroupId;

  Future<void> _submit() async {
    final selectedGroupId = _selectedGroupId;
    if (selectedGroupId == null) {
      return;
    }

    final controller = ref.read(
      attachModifierGroupControllerProvider(widget.itemId).notifier,
    );
    final succeeded = await controller.attach(
      AttachModifierGroupRequest(modifierGroupId: selectedGroupId),
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
    final allGroupsAsync = ref.watch(modifierGroupListProvider);
    final attachedGroupsAsync = ref.watch(
      itemModifierGroupListProvider(widget.itemId),
    );
    final attachState = ref.watch(
      attachModifierGroupControllerProvider(widget.itemId),
    );
    final isLoading = attachState.isLoading;
    final failure =
        ref
            .read(attachModifierGroupControllerProvider(widget.itemId).notifier)
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Attach Modifier Group: ${widget.itemName}')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 460),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.lg,
                vertical: AppSpacing.md,
              ),
              child: allGroupsAsync.when(
                loading: () => const Center(
                  child: CircularProgressIndicator(color: AppColors.brandPrimary),
                ),
                error:
                    (error, stackTrace) => Container(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      decoration: BoxDecoration(
                        color: AppColors.cardHover,
                        borderRadius: AppRadius.mdBorder,
                        border: Border.all(color: AppColors.error),
                      ),
                      child: Text(
                        'Could not load modifier groups: ${describeError(error)}',
                        style: const TextStyle(color: AppColors.error),
                      ),
                    ),
                data: (allGroups) {
                  final attachedIds =
                      attachedGroupsAsync.valueOrNull
                          ?.map((group) => group.id)
                          .toSet() ??
                      {};
                  final availableGroups =
                      allGroups
                          .where((group) => !attachedIds.contains(group.id))
                          .toList();

                  if (availableGroups.isEmpty) {
                    return Container(
                      padding: const EdgeInsets.all(AppSpacing.lg),
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: AppRadius.lgBorder,
                        border: Border.all(color: AppColors.border),
                        boxShadow: AppShadows.subtle,
                      ),
                      child: const Text(
                        'Every existing modifier group is already attached to '
                        'this item. Create a new one from Modifier Groups '
                        'first.',
                        style: TextStyle(
                          fontSize: 15,
                          color: AppColors.textSecondary,
                          height: 1.4,
                        ),
                        textAlign: TextAlign.center,
                      ),
                    );
                  }

                  return Container(
                    padding: const EdgeInsets.all(AppSpacing.lg),
                    decoration: BoxDecoration(
                      color: AppColors.surface,
                      borderRadius: AppRadius.lgBorder,
                      border: Border.all(color: AppColors.border),
                      boxShadow: AppShadows.subtle,
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        DropdownButtonFormField<String>(
                          value: _selectedGroupId,
                          decoration: InputDecoration(
                            labelText: 'Modifier group',
                            labelStyle: const TextStyle(color: AppColors.textSecondary),
                            prefixIcon: const Icon(Icons.tune, color: AppColors.brandPrimary),
                            filled: true,
                            fillColor: AppColors.background,
                            border: OutlineInputBorder(
                              borderRadius: AppRadius.mdBorder,
                              borderSide: const BorderSide(color: AppColors.border),
                            ),
                            enabledBorder: OutlineInputBorder(
                              borderRadius: AppRadius.mdBorder,
                              borderSide: const BorderSide(color: AppColors.border),
                            ),
                            focusedBorder: OutlineInputBorder(
                              borderRadius: AppRadius.mdBorder,
                              borderSide: const BorderSide(color: AppColors.brandPrimary, width: 2),
                            ),
                          ),
                          items: [
                            for (final ModifierGroup group in availableGroups)
                              DropdownMenuItem(
                                value: group.id,
                                child: Text(group.name),
                              ),
                          ],
                          onChanged:
                              isLoading
                                  ? null
                                  : (value) =>
                                      setState(() => _selectedGroupId = value),
                        ),
                        if (failure != null) ...[
                          const SizedBox(height: AppSpacing.md),
                          Container(
                            padding: const EdgeInsets.all(AppSpacing.sm),
                            decoration: BoxDecoration(
                              color: AppColors.cardHover,
                              borderRadius: AppRadius.mdBorder,
                              border: Border.all(color: AppColors.error),
                            ),
                            child: Text(
                              failure.message,
                              style: const TextStyle(
                                color: AppColors.error,
                                fontWeight: FontWeight.w500,
                              ),
                              textAlign: TextAlign.center,
                            ),
                          ),
                        ],
                        const SizedBox(height: AppSpacing.lg),
                        SizedBox(
                          height: 52,
                          child: FilledButton(
                            style: FilledButton.styleFrom(
                              backgroundColor: AppColors.brandPrimary,
                              foregroundColor: AppColors.onBrandPrimary,
                              shape: const RoundedRectangleBorder(
                                borderRadius: AppRadius.mdBorder,
                              ),
                            ),
                            onPressed:
                                (isLoading || _selectedGroupId == null)
                                    ? null
                                    : _submit,
                            child:
                                isLoading
                                    ? const SizedBox(
                                      height: 24,
                                      width: 24,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2.5,
                                        color: AppColors.onBrandPrimary,
                                      ),
                                    )
                                    : const Text(
                                      'Attach',
                                      style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w700,
                                      ),
                                    ),
                          ),
                        ),
                      ],
                    ),
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
