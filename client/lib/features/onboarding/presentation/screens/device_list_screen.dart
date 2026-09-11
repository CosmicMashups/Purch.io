import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/onboarding_providers.dart';
import 'add_device_screen.dart';

/// A3's device list. Each device's pairing code is shown plainly — the admin
/// needs to be able to read it back off-screen to type into a new tablet,
/// same as the one shown once at bootstrap time.
class DeviceListScreen extends ConsumerWidget {
  const DeviceListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final devicesAsync = ref.watch(deviceListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Devices')),
      body: devicesAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load devices: $error')),
        data: (devices) {
          if (devices.isEmpty) {
            return const Center(
              child: Text('No paired devices yet — tap + to pair one.'),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(deviceListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: devices.length,
              itemBuilder: (context, index) {
                final device = devices[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.tablet_mac)),
                  title: Text(device.deviceIdentifier ?? 'Unlabeled device'),
                  subtitle: SelectableText(
                    'Pairing code: ${device.pairingCode}',
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const AddDeviceScreen()),
            ),
        tooltip: 'Pair a device',
        child: const Icon(Icons.add),
      ),
    );
  }
}
