import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/inventory_movement_models.dart';
import '../providers/inventory_providers.dart';
import 'record_movement_screen.dart';

/// C2 — the stock movement log, filterable by movement type via the chip
/// row (matches PAGES.md's "movement type filter chips").
class MovementLogScreen extends ConsumerStatefulWidget {
  const MovementLogScreen({super.key});

  @override
  ConsumerState<MovementLogScreen> createState() => _MovementLogScreenState();
}

class _MovementLogScreenState extends ConsumerState<MovementLogScreen> {
  MovementType? _typeFilter;

  @override
  Widget build(BuildContext context) {
    final movementsAsync = ref.watch(movementLogProvider(type: _typeFilter));

    return Scaffold(
      appBar: AppBar(title: const Text('Stock Movement Log')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(12),
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  ChoiceChip(
                    label: const Text('All'),
                    selected: _typeFilter == null,
                    onSelected: (_) => setState(() => _typeFilter = null),
                  ),
                  for (final type in MovementType.values) ...[
                    const SizedBox(width: 8),
                    ChoiceChip(
                      label: Text(type.label),
                      selected: _typeFilter == type,
                      onSelected: (_) => setState(() => _typeFilter = type),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const Divider(height: 1),
          Expanded(
            child: movementsAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error:
                  (error, stackTrace) =>
                      Center(child: Text('Could not load movements: $error')),
              data: (movements) {
                if (movements.isEmpty) {
                  return const Center(
                    child: Text('No movements recorded yet.'),
                  );
                }

                return ListView.builder(
                  itemCount: movements.length,
                  itemBuilder: (context, index) {
                    final movement = movements[index];
                    final isIncrease =
                        movement.type == MovementType.stockIn ||
                        (movement.type == MovementType.adjustment &&
                            movement.quantity > 0);

                    return ListTile(
                      leading: Icon(
                        isIncrease
                            ? Icons.arrow_circle_up
                            : Icons.arrow_circle_down,
                        color: isIncrease ? Colors.green : Colors.red,
                      ),
                      title: Text(movement.itemName),
                      subtitle: Text(
                        '${movement.type.label} · ${movement.branchName} · '
                        '${movement.staffUserName}'
                        '${movement.note != null ? ' · ${movement.note}' : ''}',
                      ),
                      trailing: Text(
                        movement.quantity.toStringAsFixed(
                          movement.quantity.truncateToDouble() ==
                                  movement.quantity
                              ? 0
                              : 2,
                        ),
                      ),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const RecordMovementScreen()),
            ),
        tooltip: 'Record movement',
        child: const Icon(Icons.add),
      ),
    );
  }
}
