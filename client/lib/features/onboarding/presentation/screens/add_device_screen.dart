import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/branch_models.dart';
import '../../domain/device_models.dart';
import '../providers/onboarding_providers.dart';

String deviceTypeLabel(DeviceType type) => switch (type) {
  DeviceType.register => 'Register',
  DeviceType.kiosk => 'Self-Order Kiosk',
  DeviceType.orderBoard => 'Order Number Board',
  DeviceType.kitchenDisplay => 'Kitchen Display',
  DeviceType.warehouseOfficer => 'Warehouse Officer',
  DeviceType.customerDisplay => 'Customer Display',
};

/// Makes a device and its one-time pairing code. The code works once and expires
/// in minutes; the device is then paired by entering it (see PairDeviceScreen).
class AddDeviceScreen extends ConsumerStatefulWidget {
  const AddDeviceScreen({super.key});

  @override
  ConsumerState<AddDeviceScreen> createState() => _AddDeviceScreenState();
}

class _AddDeviceScreenState extends ConsumerState<AddDeviceScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  Branch? _selectedBranch;
  Device? _linkedRegister;
  DeviceType _deviceType = DeviceType.register;

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false) ||
        _selectedBranch == null) {
      return;
    }

    final needsRegister = _deviceType == DeviceType.customerDisplay;
    if (needsRegister && _linkedRegister == null) {
      return;
    }

    final code = await ref
        .read(createDeviceControllerProvider.notifier)
        .create(
          CreateDeviceRequest(
            name: _nameController.text.trim(),
            branchId: _selectedBranch!.id,
            deviceType: _deviceType,
            linkedRegisterDeviceId: needsRegister ? _linkedRegister!.id : null,
          ),
        );

    if (!mounted || code == null) {
      return;
    }
    await showPairingCodeDialog(context, code);
    if (mounted) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final branchesAsync = ref.watch(branchListProvider);
    final devices = ref.watch(deviceListProvider).valueOrNull ?? const [];
    final createState = ref.watch(createDeviceControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createDeviceControllerProvider.notifier).currentFailure;

    final registers =
        devices
            .where(
              (d) =>
                  d.deviceType == DeviceType.register &&
                  d.branchId == _selectedBranch?.id &&
                  d.status != DeviceStatus.revoked,
            )
            .toList();

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Add a device'), elevation: 0),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.md,
                vertical: AppSpacing.lg,
              ),
              child: Form(
                key: _formKey,
                child: Container(
                  padding: const EdgeInsets.all(AppSpacing.lg),
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.lgBorder,
                    boxShadow: AppShadows.card,
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const Text(
                        'Get a pairing code',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      const Text(
                        'Enter the code on the device. It works once and expires in a few minutes.',
                        style: TextStyle(
                          fontSize: 13,
                          color: AppColors.textSecondary,
                        ),
                      ),
                      const SizedBox(height: AppSpacing.lg),
                      TextFormField(
                        controller: _nameController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Device name',
                          hintText: 'e.g. Front counter',
                          prefixIcon: Icon(Icons.label_outline),
                        ),
                        validator:
                            (value) =>
                                (value == null || value.trim().isEmpty)
                                    ? 'Required'
                                    : null,
                      ),
                      const SizedBox(height: AppSpacing.md),
                      DropdownButtonFormField<DeviceType>(
                        value: _deviceType,
                        decoration: const InputDecoration(
                          labelText: 'Device type',
                          prefixIcon: Icon(Icons.devices_other_rounded),
                        ),
                        items:
                            DeviceType.values
                                .map(
                                  (type) => DropdownMenuItem(
                                    value: type,
                                    child: Text(deviceTypeLabel(type)),
                                  ),
                                )
                                .toList(),
                        onChanged:
                            isLoading
                                ? null
                                : (value) => setState(() {
                                  _deviceType = value ?? _deviceType;
                                  _linkedRegister = null;
                                }),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      branchesAsync.when(
                        loading:
                            () => const Center(
                              child: CircularProgressIndicator(),
                            ),
                        error: (error, _) => Text(error.toString()),
                        data:
                            (branches) => DropdownButtonFormField<Branch>(
                              value: _selectedBranch,
                              decoration: const InputDecoration(
                                labelText: 'Branch',
                                prefixIcon: Icon(Icons.location_on_outlined),
                              ),
                              items:
                                  branches
                                      .map(
                                        (branch) => DropdownMenuItem(
                                          value: branch,
                                          child: Text(branch.name),
                                        ),
                                      )
                                      .toList(),
                              onChanged:
                                  isLoading
                                      ? null
                                      : (value) => setState(() {
                                        _selectedBranch = value;
                                        _linkedRegister = null;
                                      }),
                              validator:
                                  (value) =>
                                      value == null ? 'Choose a branch' : null,
                            ),
                      ),
                      if (_deviceType == DeviceType.customerDisplay) ...[
                        const SizedBox(height: AppSpacing.md),
                        DropdownButtonFormField<Device>(
                          value: _linkedRegister,
                          decoration: const InputDecoration(
                            labelText: 'Register it shows',
                            prefixIcon: Icon(Icons.tablet_mac_rounded),
                          ),
                          items:
                              registers
                                  .map(
                                    (register) => DropdownMenuItem(
                                      value: register,
                                      child: Text(register.displayName),
                                    ),
                                  )
                                  .toList(),
                          onChanged:
                              isLoading
                                  ? null
                                  : (value) =>
                                      setState(() => _linkedRegister = value),
                          validator:
                              (value) =>
                                  value == null ? 'Choose a Register' : null,
                        ),
                      ],
                      if (failure != null) ...[
                        const SizedBox(height: AppSpacing.sm),
                        Container(
                          padding: const EdgeInsets.all(AppSpacing.sm),
                          decoration: BoxDecoration(
                            color: AppColors.error.withValues(alpha: 0.08),
                            borderRadius: AppRadius.smBorder,
                          ),
                          child: Text(
                            failure.message,
                            textAlign: TextAlign.center,
                            style: const TextStyle(
                              color: AppColors.error,
                              fontSize: 13,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                      ],
                      const SizedBox(height: AppSpacing.lg),
                      SizedBox(
                        height: 48,
                        child: FilledButton(
                          onPressed: isLoading ? null : _submit,
                          child:
                              isLoading
                                  ? const SizedBox(
                                    height: 20,
                                    width: 20,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2,
                                    ),
                                  )
                                  : const Text('Make pairing code'),
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
}

/// Shows a device's one-time pairing code. It is shown once; closing the dialog
/// means asking for a new code if it was not written down.
Future<void> showPairingCodeDialog(BuildContext context, DevicePairingCode code) {
  return showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder:
        (dialogContext) => AlertDialog(
          title: Text('Pairing code for ${code.device.displayName}'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Enter this on the device. It works once and expires soon.',
                style: TextStyle(color: AppColors.textSecondary),
              ),
              const SizedBox(height: 16),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.symmetric(vertical: 14),
                decoration: BoxDecoration(
                  color: AppColors.brandPrimaryContainer,
                  borderRadius: AppRadius.mdBorder,
                ),
                child: Center(
                  child: SelectableText(
                    code.pairingCode,
                    style: const TextStyle(
                      fontSize: 28,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 3.0,
                      color: AppColors.brandPrimary,
                    ),
                  ),
                ),
              ),
            ],
          ),
          actions: [
            FilledButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Done'),
            ),
          ],
        ),
  );
}
