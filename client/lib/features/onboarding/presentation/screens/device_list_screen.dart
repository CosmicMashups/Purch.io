import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/device_models.dart';
import '../providers/onboarding_providers.dart';
import 'add_device_screen.dart';

(IconData, String) _typeIconAndLabel(DeviceType type) => switch (type) {
  DeviceType.register => (Icons.tablet_mac_rounded, 'Register'),
  DeviceType.kiosk => (Icons.storefront_rounded, 'Self-Order Kiosk'),
  DeviceType.orderBoard => (Icons.confirmation_number_rounded, 'Order Number Board'),
  DeviceType.kitchenDisplay => (Icons.soup_kitchen_rounded, 'Kitchen Display'),
  DeviceType.warehouseOfficer => (Icons.warehouse_rounded, 'Warehouse Officer'),
};

/// A3's device list. Each device's pairing code is shown plainly — the admin
/// needs to be able to read it back off-screen to type into a new tablet,
/// same as the one shown once at bootstrap time.
class DeviceListScreen extends ConsumerWidget {
  const DeviceListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final devicesAsync = ref.watch(deviceListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Devices'),
        elevation: 0,
      ),
      body: devicesAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: error.toString(),
          onRetry: () => ref.read(deviceListProvider.notifier).refresh(),
        ),
        data: (devices) {
          if (devices.isEmpty) {
            return EmptyStateView(
              icon: Icons.devices_outlined,
              title: 'No paired devices yet — tap + to pair one.',
              description:
                  'Pair POS terminals, kitchen display systems, and customer-facing kiosks with unique pairing codes.',
              actionLabel: 'Pair New Device',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(builder: (_) => const AddDeviceScreen()),
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh: () => ref.read(deviceListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.md),
              itemCount: devices.length,
              separatorBuilder: (context, index) =>
                  const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final device = devices[index];
                final (typeIcon, typeLabel) = _typeIconAndLabel(device.deviceType);
                return Container(
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.mdBorder,
                    border: Border.all(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.xs,
                    ),
                    leading: Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        borderRadius: AppRadius.smBorder,
                      ),
                      child: Icon(typeIcon, color: AppColors.brandPrimary),
                    ),
                    title: Text(
                      device.deviceIdentifier ?? 'Unlabeled device',
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: AppSpacing.xs),
                      child: Wrap(
                        spacing: AppSpacing.xs,
                        runSpacing: AppSpacing.xs,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        children: [
                          Text(
                            typeLabel,
                            style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w700,
                              color: AppColors.textSecondary,
                            ),
                          ),
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: AppSpacing.xs,
                              vertical: 2,
                            ),
                            decoration: BoxDecoration(
                              color: AppColors.background,
                              borderRadius: AppRadius.smBorder,
                              border: Border.all(color: AppColors.border),
                            ),
                            child: SelectableText(
                              'Pairing code: ${device.pairingCode}',
                              style: const TextStyle(
                                fontFamily: 'monospace',
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
        onPressed: () => Navigator.of(context).push<void>(
          MaterialPageRoute(builder: (_) => const AddDeviceScreen()),
        ),
        tooltip: 'Pair a device',
        child: const Icon(Icons.add),
      ),
    );
  }
}

