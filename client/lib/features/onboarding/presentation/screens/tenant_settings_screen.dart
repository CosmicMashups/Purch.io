import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/purch_image.dart';
import '../../domain/tenant_settings_models.dart';
import '../providers/onboarding_providers.dart';
import 'server_connection_screen.dart';

/// A2 (branding) + A5 (BIR/compliance) + the barcode-requirement toggle + B7's
/// credit ledger ("utang") toggle, all on one screen since they're all
/// "tenant settings" from the same GET/PUT group of endpoints. Each section
/// saves independently (its own button), matching how the backend exposes
/// them as separate PUT endpoints rather than one big form submission.
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
  late final _kioskPosterUrlController = TextEditingController(
    text: widget.initial.kioskPosterImageUrl,
  );
  late final _tinController = TextEditingController(text: widget.initial.tin);
  late final _businessNameController = TextEditingController(
    text: widget.initial.registeredBusinessName,
  );
  late final _addressController = TextEditingController(
    text: widget.initial.registeredAddress,
  );
  late bool _requiresBarcode = widget.initial.requiresBarcodePerItem;
  late bool _creditLedgerEnabled = widget.initial.creditLedgerEnabled;

  @override
  void dispose() {
    _logoUrlController.dispose();
    _themeColorController.dispose();
    _fontFamilyController.dispose();
    _kioskPosterUrlController.dispose();
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
            kioskPosterImageUrl:
                _kioskPosterUrlController.text.trim().isEmpty
                    ? null
                    : _kioskPosterUrlController.text.trim(),
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

  Future<void> _toggleCreditLedger(bool value) async {
    setState(() => _creditLedgerEnabled = value);
    await ref
        .read(tenantSettingsNotifierProvider.notifier)
        .updateCreditLedgerSetting(value);
  }

  @override
  Widget build(BuildContext context) {
    final isSaving = ref.watch(tenantSettingsNotifierProvider).isLoading;
    final failure =
        ref.read(tenantSettingsNotifierProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Business Settings'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 520),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (failure != null) ...[
                    Container(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      decoration: BoxDecoration(
                        color: AppColors.error.withValues(alpha: 0.08),
                        borderRadius: AppRadius.smBorder,
                        border: Border.all(color: AppColors.error.withValues(alpha: 0.2)),
                      ),
                      child: Text(
                        failure.message,
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.error,
                        ),
                        textAlign: TextAlign.center,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),
                  ],
                  Card(
                    elevation: 0,
                    color: AppColors.surface,
                    shape: const RoundedRectangleBorder(
                      borderRadius: AppRadius.mdBorder,
                      side: BorderSide(color: AppColors.border),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(AppSpacing.lg),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Text(
                            'Branding',
                            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                              fontWeight: FontWeight.w700,
                              color: AppColors.textPrimary,
                            ),
                          ),
                          const SizedBox(height: AppSpacing.md),
                          TextField(
                            controller: _logoUrlController,
                            enabled: !isSaving,
                            decoration: const InputDecoration(
                              labelText: 'Logo URL',
                              border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
                            ),
                          ),
                          const SizedBox(height: AppSpacing.md),
                          TextField(
                            controller: _themeColorController,
                            enabled: !isSaving,
                            decoration: const InputDecoration(
                              labelText: 'Theme color (e.g. #4F46E5)',
                              border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
                            ),
                          ),
                          const SizedBox(height: AppSpacing.md),
                          TextField(
                            controller: _fontFamilyController,
                            enabled: !isSaving,
                            decoration: const InputDecoration(
                              labelText: 'Font family',
                              border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
                            ),
                          ),
                          const SizedBox(height: AppSpacing.md),
                          TextField(
                            controller: _kioskPosterUrlController,
                            enabled: !isSaving,
                            decoration: InputDecoration(
                              labelText: 'Kiosk poster image URL',
                              helperText:
                                  'Hero image on kiosk screen. Supports remote URL, /uploads/..., or assets/images/kiosk_poster_default.jpg',
                              helperMaxLines: 2,
                              suffixIcon: _kioskPosterUrlController.text.isEmpty
                                  ? TextButton(
                                      onPressed: () {
                                        _kioskPosterUrlController.text =
                                            'assets/images/kiosk_poster_default.jpg';
                                        setState(() {});
                                      },
                                      child: const Text(
                                        'Use Default',
                                        style: TextStyle(fontSize: 12),
                                      ),
                                    )
                                  : null,
                              border: const OutlineInputBorder(borderRadius: AppRadius.smBorder),
                            ),
                            onChanged: (_) => setState(() {}),
                          ),
                          // Live 16:9 preview of the poster URL
                          if (_kioskPosterUrlController.text.trim().isNotEmpty) ...[
                            const SizedBox(height: AppSpacing.sm),
                            ClipRRect(
                              borderRadius: AppRadius.smBorder,
                              child: AspectRatio(
                                aspectRatio: 16 / 9,
                                child: PurchImage(
                                  imageUrlOrPath:
                                      _kioskPosterUrlController.text.trim(),
                                  fit: BoxFit.cover,
                                  errorWidget: Container(
                                    color: AppColors.cardHover,
                                    child: const Center(
                                      child: Text(
                                        'Image could not be loaded.\nCheck the URL.',
                                        textAlign: TextAlign.center,
                                      ),
                                    ),
                                  ),
                                ),
                              ),
                            ),
                          ],
                          const SizedBox(height: AppSpacing.lg),
                          FilledButton(
                            onPressed: isSaving ? null : _saveBranding,
                            style: FilledButton.styleFrom(
                              backgroundColor: AppColors.brandPrimary,
                              shape: const RoundedRectangleBorder(borderRadius: AppRadius.smBorder),
                            ),
                            child: const Text('Save Branding'),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.lg),
                  Card(
                    elevation: 0,
                    color: AppColors.surface,
                    shape: const RoundedRectangleBorder(
                      borderRadius: AppRadius.mdBorder,
                      side: BorderSide(color: AppColors.border),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(AppSpacing.lg),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Text(
                            'BIR / Compliance',
                            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                              fontWeight: FontWeight.w700,
                              color: AppColors.textPrimary,
                            ),
                          ),
                          const SizedBox(height: AppSpacing.md),
                          TextField(
                            controller: _tinController,
                            enabled: !isSaving,
                            decoration: const InputDecoration(
                              labelText: 'TIN',
                              border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
                            ),
                          ),
                          const SizedBox(height: AppSpacing.md),
                          TextField(
                            controller: _businessNameController,
                            enabled: !isSaving,
                            decoration: const InputDecoration(
                              labelText: 'Registered business name',
                              border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
                            ),
                          ),
                          const SizedBox(height: AppSpacing.md),
                          TextField(
                            controller: _addressController,
                            enabled: !isSaving,
                            decoration: const InputDecoration(
                              labelText: 'Registered address',
                              border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
                            ),
                          ),
                          const SizedBox(height: AppSpacing.lg),
                          FilledButton(
                            onPressed: isSaving ? null : _saveBirSettings,
                            style: FilledButton.styleFrom(
                              backgroundColor: AppColors.brandPrimary,
                              shape: const RoundedRectangleBorder(borderRadius: AppRadius.smBorder),
                            ),
                            child: const Text('Save BIR Settings'),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.lg),
                  Card(
                    elevation: 0,
                    color: AppColors.surface,
                    shape: const RoundedRectangleBorder(
                      borderRadius: AppRadius.mdBorder,
                      side: BorderSide(color: AppColors.border),
                    ),
                    child: Column(
                      children: [
                        SwitchListTile(
                          title: const Text('Require a barcode for every item'),
                          value: _requiresBarcode,
                          activeColor: AppColors.brandPrimary,
                          onChanged: isSaving ? null : _toggleBarcodeRequirement,
                        ),
                        const Divider(height: 1),
                        SwitchListTile(
                          title: const Text('Offer utang / credit sales'),
                          subtitle: const Text(
                            'Lets cashiers record sales against a customer\'s credit '
                            'ledger instead of collecting payment immediately.',
                          ),
                          value: _creditLedgerEnabled,
                          activeColor: AppColors.brandPrimary,
                          onChanged: isSaving ? null : _toggleCreditLedger,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.lg),
                  OutlinedButton(
                    onPressed:
                        () => Navigator.of(context).push<void>(
                          MaterialPageRoute(
                            builder: (_) => const ServerConnectionScreen(),
                          ),
                        ),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColors.brandPrimary,
                      side: const BorderSide(color: AppColors.border),
                      shape: const RoundedRectangleBorder(borderRadius: AppRadius.smBorder),
                    ),
                    child: const Text('Local Server Connection'),
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
