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
  DeviceType.customerDisplay => (Icons.desktop_windows_rounded, 'Customer Display'),
};

/// The device list. A device waiting to be paired can be given a fresh one-time
/// code; a paired one can be revoked, after which it must be paired again.
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
                      device.displayName,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    trailing: _DeviceAction(device: device),
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
                          Text(
                            _statusLabel(device.status),
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w700,
                              color:
                                  device.status == DeviceStatus.active
                                      ? AppColors.accentEmerald
                                      : AppColors.textMuted,
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


String _statusLabel(DeviceStatus status) => switch (status) {
  DeviceStatus.active => 'Paired',
  DeviceStatus.pending => 'Waiting for its code',
  DeviceStatus.revoked => 'Revoked',
};

class _DeviceAction extends ConsumerWidget {
  const _DeviceAction({required this.device});

  final Device device;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    switch (device.status) {
      case DeviceStatus.revoked:
        return const SizedBox.shrink();
      case DeviceStatus.pending:
        return TextButton(
          onPressed: () async {
            final repository = ref.read(onboardingRepositoryProvider);
            final code = await repository.newPairingCode(device.id);
            await ref.read(deviceListProvider.notifier).refresh();
            if (context.mounted) {
              await showPairingCodeDialog(context, code);
            }
          },
          child: const Text('New code'),
        );
      case DeviceStatus.active:
        return TextButton(
          onPressed: () async {
            final confirmed = await showDialog<bool>(
              context: context,
              builder:
                  (dialogContext) => AlertDialog(
                    title: Text('Revoke ${device.displayName}?'),
                    content: const Text(
                      'It stops working at once and must be paired again with a new code.',
                    ),
                    actions: [
                      TextButton(
                        onPressed: () => Navigator.of(dialogContext).pop(false),
                        child: const Text('Cancel'),
                      ),
                      FilledButton(
                        onPressed: () => Navigator.of(dialogContext).pop(true),
                        child: const Text('Revoke'),
                      ),
                    ],
                  ),
            );
            if (confirmed ?? false) {
              await ref.read(onboardingRepositoryProvider).revokeDevice(device.id);
              await ref.read(deviceListProvider.notifier).refresh();
            }
          },
          child: const Text('Revoke'),
        );
    }
  }
}
