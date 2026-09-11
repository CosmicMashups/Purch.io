import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/item_models.dart';
import '../../domain/tingi_mode.dart';
import '../providers/catalog_providers.dart';

/// Tingi (sub-unit) selling config for a weight/volume item — e.g. a 50kg
/// sack of rice or manure that the owner allows customers to buy in smaller
/// portions. Either an explicit list of allowed sizes (FixedSizes) or a
/// step the customer can order any whole multiple of (Increment). The whole
/// pack remains sellable either way, hence PackagedSize is always required
/// once tingi is enabled.
class TingiConfigScreen extends ConsumerStatefulWidget {
  const TingiConfigScreen({super.key, required this.item});

  final Item item;

  @override
  ConsumerState<TingiConfigScreen> createState() => _TingiConfigScreenState();
}

class _SizeRow {
  _SizeRow([String initialValue = ''])
    : controller = TextEditingController(text: initialValue);

  final TextEditingController controller;

  void dispose() => controller.dispose();
}

class _TingiConfigScreenState extends ConsumerState<TingiConfigScreen> {
  late TingiMode _mode;
  final _packagedSizeController = TextEditingController();
  final _incrementStepController = TextEditingController();
  final List<_SizeRow> _sizeRows = [];

  @override
  void initState() {
    super.initState();
    _mode = widget.item.tingiMode;
    _packagedSizeController.text = widget.item.packagedSize?.toString() ?? '';
    _incrementStepController.text =
        widget.item.tingiIncrementStep?.toString() ?? '';
    final existingSizes = widget.item.tingiAllowedSizes;
    _sizeRows.addAll(
      existingSizes.isEmpty
          ? [_SizeRow()]
          : existingSizes.map((size) => _SizeRow(size.toString())),
    );
  }

  @override
  void dispose() {
    _packagedSizeController.dispose();
    _incrementStepController.dispose();
    for (final row in _sizeRows) {
      row.dispose();
    }
    super.dispose();
  }

  void _addSizeRow() {
    setState(() => _sizeRows.add(_SizeRow()));
  }

  void _removeSizeRow(int index) {
    setState(() => _sizeRows.removeAt(index).dispose());
  }

  Future<void> _submit() async {
    final controller = ref.read(
      updateTingiConfigControllerProvider(widget.item.id).notifier,
    );

    final packagedSize = double.tryParse(_packagedSizeController.text.trim());

    if (_mode != TingiMode.none &&
        (packagedSize == null || packagedSize <= 0)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Enter a valid whole pack size greater than zero.'),
        ),
      );
      return;
    }

    UpdateTingiConfigRequest request;
    switch (_mode) {
      case TingiMode.none:
        request = const UpdateTingiConfigRequest(tingiMode: TingiMode.none);
        break;

      case TingiMode.fixedSizes:
        final sizes =
            _sizeRows
                .map((row) => double.tryParse(row.controller.text.trim()))
                .whereType<double>()
                .where((size) => size > 0)
                .toList();
        if (sizes.isEmpty) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Add at least one allowed size.')),
          );
          return;
        }
        request = UpdateTingiConfigRequest(
          tingiMode: TingiMode.fixedSizes,
          packagedSize: packagedSize,
          allowedSizes: sizes,
        );
        break;

      case TingiMode.increment:
        final step = double.tryParse(_incrementStepController.text.trim());
        if (step == null || step <= 0) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Enter a valid increment step greater than zero.'),
            ),
          );
          return;
        }
        request = UpdateTingiConfigRequest(
          tingiMode: TingiMode.increment,
          packagedSize: packagedSize,
          tingiIncrementStep: step,
        );
        break;
    }

    final succeeded = await controller.updateTingiConfig(request);

    if (!mounted) {
      return;
    }

    if (succeeded) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final updateState = ref.watch(
      updateTingiConfigControllerProvider(widget.item.id),
    );
    final isLoading = updateState.isLoading;
    final failure =
        ref
            .read(updateTingiConfigControllerProvider(widget.item.id).notifier)
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Tingi Selling: ${widget.item.name}')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(32),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SegmentedButton<TingiMode>(
                    segments: const [
                      ButtonSegment(
                        value: TingiMode.none,
                        label: Text('Whole only'),
                      ),
                      ButtonSegment(
                        value: TingiMode.fixedSizes,
                        label: Text('Fixed sizes'),
                      ),
                      ButtonSegment(
                        value: TingiMode.increment,
                        label: Text('Increment'),
                      ),
                    ],
                    selected: {_mode},
                    onSelectionChanged:
                        isLoading
                            ? null
                            : (selection) =>
                                setState(() => _mode = selection.first),
                  ),
                  if (_mode != TingiMode.none) ...[
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _packagedSizeController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Whole pack size (e.g. 50 for a 50kg sack)',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                    ),
                  ],
                  if (_mode == TingiMode.fixedSizes) ...[
                    const SizedBox(height: 16),
                    Text(
                      'Allowed sizes',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 8),
                    for (var index = 0; index < _sizeRows.length; index++) ...[
                      Row(
                        children: [
                          Expanded(
                            child: TextFormField(
                              controller: _sizeRows[index].controller,
                              enabled: !isLoading,
                              decoration: const InputDecoration(
                                labelText: 'Size (e.g. 10)',
                                border: OutlineInputBorder(),
                              ),
                              keyboardType:
                                  const TextInputType.numberWithOptions(
                                    decimal: true,
                                  ),
                            ),
                          ),
                          IconButton(
                            onPressed:
                                isLoading || _sizeRows.length == 1
                                    ? null
                                    : () => _removeSizeRow(index),
                            icon: const Icon(Icons.remove_circle_outline),
                            tooltip: 'Remove size',
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                    ],
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton.icon(
                        onPressed: isLoading ? null : _addSizeRow,
                        icon: const Icon(Icons.add),
                        label: const Text('Add size'),
                      ),
                    ),
                  ],
                  if (_mode == TingiMode.increment) ...[
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _incrementStepController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Increment step (e.g. 5)',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                    ),
                  ],
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
                      onPressed: isLoading ? null : _submit,
                      child:
                          isLoading
                              ? const SizedBox(
                                height: 24,
                                width: 24,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2.5,
                                ),
                              )
                              : const Text('Save'),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
