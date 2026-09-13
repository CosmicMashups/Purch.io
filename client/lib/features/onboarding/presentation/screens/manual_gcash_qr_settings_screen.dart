import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/purch_image.dart';
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
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Manual GCash QR: ${widget.branch.name}'),
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.md,
                vertical: AppSpacing.md,
              ),
              child: Container(
                padding: const EdgeInsets.all(AppSpacing.md),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: AppRadius.mdBorder,
                  border: Border.all(color: AppColors.border),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Container(
                      padding: const EdgeInsets.all(AppSpacing.sm),
                      decoration: BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        borderRadius: AppRadius.smBorder,
                        border: Border.all(
                          color: AppColors.brandPrimary.withOpacity(0.2),
                        ),
                      ),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Icon(
                            Icons.info_outline,
                            color: AppColors.brandPrimary,
                            size: 20,
                          ),
                          const SizedBox(width: AppSpacing.xs),
                          Expanded(
                            child: Text(
                              'Let customers pay this branch directly through GCash '
                              '(or any QR Ph-compatible app) by scanning your own QR '
                              'code — no gateway fee, but you\'ll need to confirm each '
                              'payment yourself before marking it received.',
                              style: TextStyle(
                                fontSize: 12,
                                color: AppColors.textSecondary,
                                height: 1.35,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    if (_qrImageUrlController.text.isNotEmpty) ...[
                      Center(
                        child: Container(
                          padding: const EdgeInsets.all(AppSpacing.xs),
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: AppRadius.smBorder,
                            border: Border.all(color: AppColors.border),
                          ),
                          child: ClipRRect(
                            borderRadius: AppRadius.smBorder,
                            child: PurchImage(
                              imageUrlOrPath: _qrImageUrlController.text.trim(),
                              height: 160,
                              fit: BoxFit.contain,
                              errorWidget: const SizedBox.shrink(),
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(height: AppSpacing.md),
                    ],
                    TextField(
                      controller: _qrImageUrlController,
                      enabled: !isSaving,
                      decoration: InputDecoration(
                        labelText: 'QR code image URL',
                        hintText: 'https://example.com/qr.png or /uploads/...',
                        prefixIcon: const Icon(Icons.qr_code_2_rounded),
                        suffixIcon: _qrImageUrlController.text.isEmpty
                            ? TextButton(
                                onPressed: () {
                                  _qrImageUrlController.text =
                                      'assets/images/sample_qr_ph.jpg';
                                  setState(() {});
                                },
                                child: const Text(
                                  'Use Sample',
                                  style: TextStyle(fontSize: 12),
                                ),
                              )
                            : null,
                        border: const OutlineInputBorder(),
                        isDense: true,
                      ),
                      onChanged: (_) => setState(() {}),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    TextField(
                      controller: _accountNameController,
                      enabled: !isSaving,
                      decoration: const InputDecoration(
                        labelText: 'GCash account name',
                        hintText: 'e.g. Juan dela Cruz',
                        prefixIcon: Icon(Icons.person_outline),
                        border: OutlineInputBorder(),
                        isDense: true,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    TextField(
                      controller: _accountNumberController,
                      enabled: !isSaving,
                      decoration: const InputDecoration(
                        labelText: 'GCash number',
                        hintText: '09xx-xxx-xxxx',
                        prefixIcon: Icon(Icons.phone_android_outlined),
                        border: OutlineInputBorder(),
                        isDense: true,
                      ),
                    ),
                    if (failure != null) ...[
                      const SizedBox(height: AppSpacing.sm),
                      Container(
                        padding: const EdgeInsets.all(AppSpacing.sm),
                        decoration: BoxDecoration(
                          color: AppColors.error.withOpacity(0.08),
                          borderRadius: AppRadius.smBorder,
                          border: Border.all(
                            color: AppColors.error.withOpacity(0.2),
                          ),
                        ),
                        child: Text(
                          failure.message,
                          style: const TextStyle(
                            color: AppColors.error,
                            fontSize: 13,
                            fontWeight: FontWeight.w500,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ),
                    ],
                    const SizedBox(height: AppSpacing.md),
                    SizedBox(
                      height: 48,
                      child: FilledButton(
                        onPressed: isSaving ? null : _submit,
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColors.brandPrimary,
                          shape: RoundedRectangleBorder(
                            borderRadius: AppRadius.smBorder,
                          ),
                        ),
                        child:
                            isSaving
                                ? const SizedBox(
                                  height: 20,
                                  width: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                                : const Text(
                                  'Save',
                                  style: TextStyle(
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
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

