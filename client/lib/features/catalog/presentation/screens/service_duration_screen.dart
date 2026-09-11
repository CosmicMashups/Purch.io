import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/item_models.dart';
import '../providers/catalog_providers.dart';

/// B2c — appointment/service length in minutes, for service-priced items
/// (e.g. a 45-minute haircut). Prefills from the item's existing duration.
class ServiceDurationScreen extends ConsumerStatefulWidget {
  const ServiceDurationScreen({super.key, required this.item});

  final Item item;

  @override
  ConsumerState<ServiceDurationScreen> createState() =>
      _ServiceDurationScreenState();
}

class _ServiceDurationScreenState extends ConsumerState<ServiceDurationScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _durationController;

  @override
  void initState() {
    super.initState();
    _durationController = TextEditingController(
      text: widget.item.serviceDurationMinutes?.toString() ?? '',
    );
  }

  @override
  void dispose() {
    _durationController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(
      updateServiceDurationControllerProvider(widget.item.id).notifier,
    );
    final succeeded = await controller.updateServiceDuration(
      UpdateServiceDurationRequest(
        durationMinutes: int.parse(_durationController.text.trim()),
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
    final updateState = ref.watch(
      updateServiceDurationControllerProvider(widget.item.id),
    );
    final isLoading = updateState.isLoading;
    final failure =
        ref
            .read(
              updateServiceDurationControllerProvider(widget.item.id).notifier,
            )
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Service Duration: ${widget.item.name}')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(32),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextFormField(
                      controller: _durationController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Duration (minutes)',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: TextInputType.number,
                      validator: (value) {
                        final parsed = int.tryParse(value?.trim() ?? '');
                        if (parsed == null || parsed <= 0) {
                          return 'Enter a duration greater than zero';
                        }
                        return null;
                      },
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
      ),
    );
  }
}
