import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/branch_models.dart';
import '../../domain/device_models.dart';
import '../providers/onboarding_providers.dart';
import '../../../../core/errors/failure.dart';

class AddDeviceScreen extends ConsumerStatefulWidget {
  const AddDeviceScreen({super.key});

  @override
  ConsumerState<AddDeviceScreen> createState() => _AddDeviceScreenState();
}

class _AddDeviceScreenState extends ConsumerState<AddDeviceScreen> {
  final _formKey = GlobalKey<FormState>();
  final _identifierController = TextEditingController();
  final _pairingPinController = TextEditingController();
  Branch? _selectedBranch;
  DeviceType _deviceType = DeviceType.register;

  @override
  void dispose() {
    _identifierController.dispose();
    _pairingPinController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false) ||
        _selectedBranch == null) {
      return;
    }

    final controller = ref.read(createDeviceControllerProvider.notifier);
    final succeeded = await controller.create(
      CreateDeviceRequest(
        branchId: _selectedBranch!.id,
        deviceIdentifier:
            _identifierController.text.trim().isEmpty
                ? null
                : _identifierController.text.trim(),
        deviceType: _deviceType,
        pairingPin:
            _deviceType == DeviceType.register
                ? null
                : _pairingPinController.text.trim(),
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
    final createState = ref.watch(createDeviceControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createDeviceControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Set Up This Device'),
        elevation: 0,
      ),
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
                      Row(
                        children: [
                          Container(
                            width: 44,
                            height: 44,
                            decoration: BoxDecoration(
                              color: AppColors.brandPrimaryContainer,
                              borderRadius: AppRadius.mdBorder,
                            ),
                            child: const Icon(
                              Icons.tablet_mac_rounded,
                              color: AppColors.brandPrimary,
                              size: 22,
                            ),
                          ),
                          const SizedBox(width: AppSpacing.md),
                          const Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  'Pair this device',
                                  style: TextStyle(
                                    fontSize: 18,
                                    fontWeight: FontWeight.w800,
                                    color: AppColors.textPrimary,
                                  ),
                                ),
                                Text(
                                  'Choose a branch to get a pairing code for the login screen',
                                  style: TextStyle(
                                    fontSize: 13,
                                    color: AppColors.textSecondary,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.lg),
                      branchesAsync.when(
                        loading:
                            () => const Center(
                              child: CircularProgressIndicator(
                                color: AppColors.brandPrimary,
                              ),
                            ),
                        error:
                            (error, stackTrace) => Text(
                              'Could not load branches: ${describeError(error)}',
                              style: const TextStyle(color: AppColors.error),
                            ),
                        data: (branches) {
                          _selectedBranch ??=
                              branches.isNotEmpty ? branches.first : null;
                          return DropdownButtonFormField<Branch>(
                            value: _selectedBranch,
                            isExpanded: true,
                            decoration: const InputDecoration(
                              labelText: 'Branch',
                              prefixIcon: Icon(Icons.storefront_outlined),
                              border: OutlineInputBorder(),
                              isDense: true,
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
                                    : (value) =>
                                        setState(() => _selectedBranch = value),
                            validator:
                                (value) => value == null ? 'Required' : null,
                          );
                        },
                      ),
                      const SizedBox(height: AppSpacing.md),
                      TextFormField(
                        controller: _identifierController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Device label (optional, e.g. "Tablet 2")',
                          hintText: 'e.g. Counter 1 Tablet',
                          prefixIcon: Icon(Icons.tablet_mac_outlined),
                          border: OutlineInputBorder(),
                          isDense: true,
                        ),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      DropdownButtonFormField<DeviceType>(
                        value: _deviceType,
                        isExpanded: true,
                        decoration: const InputDecoration(
                          labelText: 'Device type',
                          prefixIcon: Icon(Icons.devices_other_outlined),
                          border: OutlineInputBorder(),
                          isDense: true,
                        ),
                        items: const [
                          DropdownMenuItem(
                            value: DeviceType.register,
                            child: Text('Register (staff login)'),
                          ),
                          DropdownMenuItem(
                            value: DeviceType.kiosk,
                            child: Text('Self-Order Kiosk'),
                          ),
                          DropdownMenuItem(
                            value: DeviceType.orderBoard,
                            child: Text('Order Number Board'),
                          ),
                          DropdownMenuItem(
                            value: DeviceType.kitchenDisplay,
                            child: Text('Kitchen Display'),
                          ),
                          DropdownMenuItem(
                            value: DeviceType.warehouseOfficer,
                            child: Text('Warehouse Officer (Home + Inventory)'),
                          ),
                        ],
                        onChanged:
                            isLoading
                                ? null
                                : (value) => setState(
                                  () => _deviceType = value ?? DeviceType.register,
                                ),
                      ),
                      if (_deviceType != DeviceType.register) ...[
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _pairingPinController,
                          enabled: !isLoading,
                          decoration: const InputDecoration(
                            labelText: 'Pairing PIN',
                            helperText: 'Given to whoever sets up this device — required alongside the pairing code above.',
                            prefixIcon: Icon(Icons.pin_outlined),
                            border: OutlineInputBorder(),
                            isDense: true,
                          ),
                          keyboardType: TextInputType.number,
                          validator:
                              (value) =>
                                  (value == null || value.trim().isEmpty)
                                      ? 'Required for this device type'
                                      : null,
                        ),
                      ],
                      if (failure != null) ...[
                        const SizedBox(height: AppSpacing.sm),
                        Container(
                          padding: const EdgeInsets.all(AppSpacing.sm),
                          decoration: BoxDecoration(
                            color: AppColors.error.withValues(alpha: 0.08),
                            borderRadius: AppRadius.smBorder,
                            border: Border.all(
                              color: AppColors.error.withValues(alpha: 0.2),
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
                          onPressed: isLoading ? null : _submit,
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            shape: RoundedRectangleBorder(
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
                                  : const Text(
                                    'Pair Device',
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
      ),
    );
  }
}

