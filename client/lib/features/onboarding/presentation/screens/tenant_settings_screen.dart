import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/tenant_settings_models.dart';
import '../providers/onboarding_providers.dart';

/// A2 (branding) + A5 (BIR/compliance) + the barcode-requirement toggle, all
/// on one screen since they're all "tenant settings" from the same GET/PUT
/// group of endpoints. Each section saves independently (its own button),
/// matching how the backend exposes them as three separate PUT endpoints
/// rather than one big form submission.
class TenantSettingsScreen extends ConsumerWidget {
  const TenantSettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final settingsAsync = ref.watch(tenantSettingsNotifierProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Business Settings')),
      body: settingsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load settings: $error')),
        // Keyed by tenant id so the form's local controllers only re-seed if
        // we somehow load a genuinely different tenant, not on every rebuild.
        data:
            (settings) => _TenantSettingsForm(
              key: ValueKey(settings.id),
              initial: settings,
            ),
      ),
    );
  }
}

class _TenantSettingsForm extends ConsumerStatefulWidget {
  const _TenantSettingsForm({super.key, required this.initial});

  final TenantSettings initial;

  @override
  ConsumerState<_TenantSettingsForm> createState() =>
      _TenantSettingsFormState();
}

class _TenantSettingsFormState extends ConsumerState<_TenantSettingsForm> {
  late final _logoUrlController = TextEditingController(
    text: widget.initial.brandingLogoUrl,
  );
  late final _themeColorController = TextEditingController(
    text: widget.initial.brandingThemeColorHex,
  );
  late final _fontFamilyController = TextEditingController(
    text: widget.initial.brandingFontFamily,
  );
  late final _tinController = TextEditingController(text: widget.initial.tin);
  late final _businessNameController = TextEditingController(
    text: widget.initial.registeredBusinessName,
  );
  late final _addressController = TextEditingController(
    text: widget.initial.registeredAddress,
  );
  late bool _requiresBarcode = widget.initial.requiresBarcodePerItem;

  @override
  void dispose() {
    _logoUrlController.dispose();
    _themeColorController.dispose();
    _fontFamilyController.dispose();
    _tinController.dispose();
    _businessNameController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  Future<void> _saveBranding() async {
    await ref
        .read(tenantSettingsNotifierProvider.notifier)
        .updateBranding(
          UpdateBrandingRequest(
            logoUrl:
                _logoUrlController.text.trim().isEmpty
                    ? null
                    : _logoUrlController.text.trim(),
            themeColorHex:
                _themeColorController.text.trim().isEmpty
                    ? null
                    : _themeColorController.text.trim(),
            fontFamily:
                _fontFamilyController.text.trim().isEmpty
                    ? null
                    : _fontFamilyController.text.trim(),
          ),
        );
  }

  Future<void> _saveBirSettings() async {
    await ref
        .read(tenantSettingsNotifierProvider.notifier)
        .updateBirSettings(
          UpdateBirSettingsRequest(
            tin:
                _tinController.text.trim().isEmpty
                    ? null
                    : _tinController.text.trim(),
            registeredBusinessName:
                _businessNameController.text.trim().isEmpty
                    ? null
                    : _businessNameController.text.trim(),
            registeredAddress:
                _addressController.text.trim().isEmpty
                    ? null
                    : _addressController.text.trim(),
          ),
        );
  }

  Future<void> _toggleBarcodeRequirement(bool value) async {
    setState(() => _requiresBarcode = value);
    await ref
        .read(tenantSettingsNotifierProvider.notifier)
        .updateBarcodeSetting(value);
  }

  @override
  Widget build(BuildContext context) {
    final isSaving = ref.watch(tenantSettingsNotifierProvider).isLoading;
    final failure =
        ref.read(tenantSettingsNotifierProvider.notifier).currentFailure;

    return SafeArea(
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 480),
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (failure != null) ...[
                  Text(
                    failure.message,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 16),
                ],
                Text('Branding', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 12),
                TextField(
                  controller: _logoUrlController,
                  enabled: !isSaving,
                  decoration: const InputDecoration(
                    labelText: 'Logo URL',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _themeColorController,
                  enabled: !isSaving,
                  decoration: const InputDecoration(
                    labelText: 'Theme color (e.g. #4F46E5)',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _fontFamilyController,
                  enabled: !isSaving,
                  decoration: const InputDecoration(
                    labelText: 'Font family',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: isSaving ? null : _saveBranding,
                  child: const Text('Save Branding'),
                ),
                const SizedBox(height: 32),
                Text(
                  'BIR / Compliance',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _tinController,
                  enabled: !isSaving,
                  decoration: const InputDecoration(
                    labelText: 'TIN',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _businessNameController,
                  enabled: !isSaving,
                  decoration: const InputDecoration(
                    labelText: 'Registered business name',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _addressController,
                  enabled: !isSaving,
                  decoration: const InputDecoration(
                    labelText: 'Registered address',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: isSaving ? null : _saveBirSettings,
                  child: const Text('Save BIR Settings'),
                ),
                const SizedBox(height: 32),
                SwitchListTile(
                  title: const Text('Require a barcode for every item'),
                  value: _requiresBarcode,
                  onChanged: isSaving ? null : _toggleBarcodeRequirement,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
