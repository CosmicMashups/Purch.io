import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/server_connection_providers.dart';

/// Phase 10's "manual-IP-entry" flow: a Local/on-prem installation runs one
/// backend on the store's own LAN, and every physical device (cashier POS,
/// kiosk terminal) needs pointing at that server's address once. No
/// LAN-discovery (mDNS) is implemented — that would need a new plugin
/// dependency; this is the dependency-free manual-entry half of Phase 10's
/// either/or requirement, with discovery flagged as a deferred enhancement.
class ServerConnectionScreen extends ConsumerStatefulWidget {
  const ServerConnectionScreen({super.key});

  @override
  ConsumerState<ServerConnectionScreen> createState() =>
      _ServerConnectionScreenState();
}

class _ServerConnectionScreenState
    extends ConsumerState<ServerConnectionScreen> {
  final _formKey = GlobalKey<FormState>();
  final _addressController = TextEditingController();
  bool _savedJustNow = false;

  @override
  void dispose() {
    _addressController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    setState(() => _savedJustNow = false);

    final controller = ref.read(serverConnectionControllerProvider.notifier);
    final succeeded = await controller.testAndSave(
      _addressController.text.trim(),
    );

    if (!mounted) {
      return;
    }

    setState(() => _savedJustNow = succeeded);
  }

  @override
  Widget build(BuildContext context) {
    final connectionState = ref.watch(serverConnectionControllerProvider);
    final isLoading = connectionState.isLoading;
    final failure =
        ref.read(serverConnectionControllerProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(title: const Text('Connect to a Local Server')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(32),
              child: Form(
                key: _formKey,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Icon(Icons.router, size: 64),
                    const SizedBox(height: 16),
                    const Text(
                      'For a Local/on-prem installation, enter the IP '
                      'address (and port) of the server running on this '
                      'store\'s network — for example 192.168.1.50:5000. '
                      'Ask whoever set up the installer if you\'re not sure.',
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 24),
                    TextFormField(
                      controller: _addressController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Server address',
                        hintText: '192.168.1.50:5000',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: TextInputType.url,
                      onFieldSubmitted: (_) => _submit(),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
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
                    if (_savedJustNow) ...[
                      const SizedBox(height: 16),
                      const Text(
                        'Connected — this device will use that server from now on.',
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
                                : const Text('Test & Save'),
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
