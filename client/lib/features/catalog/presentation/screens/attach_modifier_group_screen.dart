import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/modifier_models.dart';
import '../providers/catalog_providers.dart';

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
            constraints: const BoxConstraints(maxWidth: 420),
            child: Padding(
              padding: const EdgeInsets.all(32),
              child: allGroupsAsync.when(
                loading: () => const CircularProgressIndicator(),
                error:
                    (error, stackTrace) =>
                        Text('Could not load modifier groups: $error'),
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
                    return const Text(
                      'Every existing modifier group is already attached to '
                      'this item. Create a new one from Modifier Groups '
                      'first.',
                    );
                  }

                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      DropdownButtonFormField<String>(
                        value: _selectedGroupId,
                        decoration: const InputDecoration(
                          labelText: 'Modifier group',
                          border: OutlineInputBorder(),
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
                                    ),
                                  )
                                  : const Text('Attach'),
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
