import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/branch_models.dart';
import '../providers/onboarding_providers.dart';

/// D5 — a merchant-uploaded static QR Ph code (e.g. GCash's own "receive
/// money" QR), paid directly into the tenant's own account. No gateway, no
/// per-transaction fee, and no webhook confirmation — the cashier verifies
/// receipt manually at checkout, the same trust model as Bank Transfer.
/// Independent of Xendit's dynamic QR Ph.
class ManualGcashQrSettingsScreen extends ConsumerStatefulWidget {
  const ManualGcashQrSettingsScreen({super.key, required this.branch});

  final Branch branch;

  @override
  ConsumerState<ManualGcashQrSettingsScreen> createState() =>
      _ManualGcashQrSettingsScreenState();
}

class _ManualGcashQrSettingsScreenState
    extends ConsumerState<ManualGcashQrSettingsScreen> {
  late final _qrImageUrlController = TextEditingController(
    text: widget.branch.manualGcashQrImageUrl,
  );
  late final _accountNameController = TextEditingController(
    text: widget.branch.manualGcashAccountName,
  );
  late final _accountNumberController = TextEditingController(
    text: widget.branch.manualGcashAccountNumber,
  );

  @override
  void dispose() {
    _qrImageUrlController.dispose();
    _accountNameController.dispose();
    _accountNumberController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final controller = ref.read(
      manualGcashQrSettingsControllerProvider.notifier,
    );
    final succeeded = await controller.updateSettings(
      widget.branch.id,
      UpdateManualGcashQrSettingsRequest(
        qrImageUrl:
            _qrImageUrlController.text.trim().isEmpty
                ? null
                : _qrImageUrlController.text.trim(),
        accountName:
            _accountNameController.text.trim().isEmpty
                ? null
                : _accountNameController.text.trim(),
        accountNumber:
            _accountNumberController.text.trim().isEmpty
                ? null
                : _accountNumberController.text.trim(),
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
    final updateState = ref.watch(manualGcashQrSettingsControllerProvider);
    final isSaving = updateState.isLoading;
    final failure =
        ref
            .read(manualGcashQrSettingsControllerProvider.notifier)
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Manual GCash QR: ${widget.branch.name}')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Let customers pay this branch directly through GCash '
                    '(or any QR Ph-compatible app) by scanning your own QR '
                    'code — no gateway fee, but you\'ll need to confirm each '
                    'payment yourself before marking it received.',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  const SizedBox(height: 16),
                  if (_qrImageUrlController.text.isNotEmpty) ...[
                    Image.network(
                      _qrImageUrlController.text,
                      height: 180,
                      errorBuilder:
                          (context, error, stackTrace) =>
                              const SizedBox.shrink(),
                    ),
                    const SizedBox(height: 16),
                  ],
                  TextField(
                    controller: _qrImageUrlController,
                    enabled: !isSaving,
                    decoration: const InputDecoration(
                      labelText: 'QR code image URL',
                      border: OutlineInputBorder(),
                    ),
                    onChanged: (_) => setState(() {}),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _accountNameController,
                    enabled: !isSaving,
                    decoration: const InputDecoration(
                      labelText: 'GCash account name',
                      border: OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _accountNumberController,
                    enabled: !isSaving,
                    decoration: const InputDecoration(
                      labelText: 'GCash number',
                      border: OutlineInputBorder(),
                    ),
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
                      onPressed: isSaving ? null : _submit,
                      child:
                          isSaving
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
