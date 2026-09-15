import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_fonts/google_fonts.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/theming/theme_builder.dart';
import '../../../../core/widgets/image_upload_field.dart';
import '../../../../core/widgets/purch_image.dart';
import '../../../legal/presentation/screens/privacy_policy_screen.dart';
import '../../../legal/presentation/screens/terms_of_service_screen.dart';
import '../../domain/tenant_settings_models.dart';
import '../providers/onboarding_providers.dart';
import 'server_connection_screen.dart';
import '../../../../core/errors/failure.dart';

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

    // _TenantSettingsForm owns the single Scaffold/AppBar for this route —
    // this widget only exists to branch on the settings load state, so it
    // must not add a second AppBar above it (that showed up as two "Business
    // Settings" top bars stacked on screen).
    return settingsAsync.when(
      loading:
          () => const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          ),
      error:
          (error, stackTrace) => Scaffold(
            appBar: AppBar(title: const Text('Business Settings')),
            body: Center(
              child: Text('Could not load settings: ${describeError(error)}'),
            ),
          ),
      // Keyed by tenant id so the form's local controllers only re-seed if
      // we somehow load a genuinely different tenant, not on every rebuild.
      data:
          (settings) => _TenantSettingsForm(
            key: ValueKey(settings.id),
            initial: settings,
          ),
    );
  }
}

/// One branding colour input. Same visual treatment as the other fields on
/// this form, plus a live swatch so an admin can see what they typed before
/// saving. Blank = "use the app default"; an unparsable value simply shows no
/// swatch (the backend rejects it on save, and the theme falls back at runtime).
class _ColorField extends StatefulWidget {
  const _ColorField({
    required this.controller,
    required this.label,
    required this.enabled,
  });

  final TextEditingController controller;
  final String label;
  final bool enabled;

  @override
  State<_ColorField> createState() => _ColorFieldState();
}

class _ColorFieldState extends State<_ColorField> {
  @override
  Widget build(BuildContext context) {
    final parsed = parseHexColor(widget.controller.text.trim());

    return TextField(
      controller: widget.controller,
      enabled: widget.enabled,
      onChanged: (_) => setState(() {}),
      decoration: InputDecoration(
        labelText: widget.label,
        helperText: 'Leave blank to use the app default.',
        border: const OutlineInputBorder(borderRadius: AppRadius.smBorder),
        suffixIcon:
            parsed == null
                ? null
                : Padding(
                  padding: const EdgeInsets.all(AppSpacing.md),
                  child: Container(
                    width: 20,
                    height: 20,
                    decoration: BoxDecoration(
                      color: parsed,
                      borderRadius: AppRadius.smBorder,
                      border: Border.all(color: AppColors.border),
                    ),
                  ),
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
  late final _backgroundColorController = TextEditingController(
    text: widget.initial.brandingBackgroundColorHex,
  );
  late final _accentColorController = TextEditingController(
    text: widget.initial.brandingAccentColorHex,
  );
  late final _primaryTextColorController = TextEditingController(
    text: widget.initial.brandingPrimaryTextColorHex,
  );
  late final _secondaryTextColorController = TextEditingController(
    text: widget.initial.brandingSecondaryTextColorHex,
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
    _backgroundColorController.dispose();
    _accentColorController.dispose();
    _primaryTextColorController.dispose();
    _secondaryTextColorController.dispose();
    _fontFamilyController.dispose();
    _kioskPosterUrlController.dispose();
    _tinController.dispose();
    _businessNameController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  static String? _nullIfBlank(TextEditingController controller) {
    final trimmed = controller.text.trim();
    return trimmed.isEmpty ? null : trimmed;
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
            backgroundColorHex: _nullIfBlank(_backgroundColorController),
            accentColorHex: _nullIfBlank(_accentColorController),
            primaryTextColorHex: _nullIfBlank(_primaryTextColorController),
            secondaryTextColorHex: _nullIfBlank(_secondaryTextColorController),
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
                          ImageUploadField(
                            controller: _logoUrlController,
                            label: 'Logo',
                            enabled: !isSaving,
                            onChanged: () => setState(() {}),
                          ),
                          const SizedBox(height: AppSpacing.md),
                          _ColorField(
                            controller: _backgroundColorController,
                            label: 'Background color (e.g. #F8FAFC)',
                            enabled: !isSaving,
                          ),
                          const SizedBox(height: AppSpacing.md),
                          _ColorField(
                            controller: _accentColorController,
                            label: 'Accent color (e.g. #1E40AF)',
                            enabled: !isSaving,
                          ),
                          const SizedBox(height: AppSpacing.md),
                          _ColorField(
                            controller: _primaryTextColorController,
                            label: 'Primary text color (e.g. #0F172A)',
                            enabled: !isSaving,
                          ),
                          const SizedBox(height: AppSpacing.md),
                          _ColorField(
                            controller: _secondaryTextColorController,
                            label: 'Secondary text color (e.g. #475569)',
                            enabled: !isSaving,
                          ),
                          const SizedBox(height: AppSpacing.md),
                          DropdownButtonFormField<String>(
                            isExpanded: true,
                            value: AppTypography.curatedPosFonts.contains(
                              _fontFamilyController.text.trim(),
                            )
                                ? _fontFamilyController.text.trim()
                                : (_fontFamilyController.text.trim().isEmpty
                                    ? AppTypography.defaultFontFamily
                                    : 'CUSTOM'),
                            decoration: const InputDecoration(
                              labelText: 'Curated POS Font Preset',
                              border: OutlineInputBorder(
                                borderRadius: AppRadius.smBorder,
                              ),
                            ),
                            items: [
                              ...AppTypography.curatedPosFonts.map(
                                (f) => DropdownMenuItem(
                                  value: f,
                                  child: Text(
                                    f == AppTypography.defaultFontFamily
                                        ? '$f (Default)'
                                        : f,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                              ),
                              const DropdownMenuItem(
                                value: 'CUSTOM',
                                child: Text('Custom Google Font...'),
                              ),
                            ],
                            onChanged:
                                isSaving
                                    ? null
                                    : (val) {
                                      if (val != null && val != 'CUSTOM') {
                                        _fontFamilyController.text = val;
                                        setState(() {});
                                      }
                                    },
                          ),
                          const SizedBox(height: AppSpacing.sm),
                          TextField(
                            controller: _fontFamilyController,
                            enabled: !isSaving,
                            decoration: const InputDecoration(
                              labelText: 'Font family',
                              helperText:
                                  'Select a curated POS preset above or enter any valid Google Font name.',
                              border: OutlineInputBorder(
                                borderRadius: AppRadius.smBorder,
                              ),
                            ),
                            onChanged: (_) => setState(() {}),
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
                              suffixIcon:
                                  _kioskPosterUrlController.text.isEmpty
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
                              border: const OutlineInputBorder(
                                borderRadius: AppRadius.smBorder,
                              ),
                            ),
                            onChanged: (_) => setState(() {}),
                          ),
                          // Live 16:9 preview of the poster URL
                          if (_kioskPosterUrlController.text
                              .trim()
                              .isNotEmpty) ...[
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
                              shape: const RoundedRectangleBorder(
                                borderRadius: AppRadius.smBorder,
                              ),
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
                      padding: const EdgeInsets.all(AppSpacing.md),
                      child: _TypographyPreviewCard(
                        fontFamily: _fontFamilyController.text,
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
                        ListTile(
                          leading: const Icon(
                            Icons.privacy_tip_outlined,
                            color: AppColors.textSecondary,
                          ),
                          title: const Text('Privacy Policy'),
                          trailing: const Icon(
                            Icons.chevron_right_rounded,
                            color: AppColors.textMuted,
                          ),
                          onTap:
                              () => Navigator.of(context).push<void>(
                                MaterialPageRoute(
                                  builder: (_) => const PrivacyPolicyScreen(),
                                ),
                              ),
                        ),
                        const Divider(height: 1),
                        ListTile(
                          leading: const Icon(
                            Icons.gavel_outlined,
                            color: AppColors.textSecondary,
                          ),
                          title: const Text('Terms of Service'),
                          trailing: const Icon(
                            Icons.chevron_right_rounded,
                            color: AppColors.textMuted,
                          ),
                          onTap:
                              () => Navigator.of(context).push<void>(
                                MaterialPageRoute(
                                  builder: (_) => const TermsOfServiceScreen(),
                                ),
                              ),
                        ),
                      ],
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

/// Interactive live typography preview card for tenant administrators.
class _TypographyPreviewCard extends StatelessWidget {
  const _TypographyPreviewCard({required this.fontFamily});

  final String fontFamily;

  @override
  Widget build(BuildContext context) {
    final trimmed = fontFamily.trim();
    final effectiveFamily =
        trimmed.isEmpty ? AppTypography.defaultFontFamily : trimmed;

    final headerStyle = AppTypography.getSafeGoogleFont(
      effectiveFamily,
      fontSize: 18,
      fontWeight: FontWeight.w700,
      color: AppColors.textPrimary,
      letterSpacing: -0.2,
    );

    final bodyStyle = AppTypography.getSafeGoogleFont(
      effectiveFamily,
      fontSize: 13,
      fontWeight: FontWeight.w500,
      color: AppColors.textSecondary,
    );

    final priceStyle = GoogleFonts.jetBrainsMono(
      fontSize: 15,
      fontWeight: FontWeight.w700,
      color: AppColors.brandPrimary,
    );

    return Container(
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: AppRadius.smBorder,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                mainAxisSize: MainAxisSize.min,
                children: const [
                  Icon(
                    Icons.font_download_outlined,
                    size: 15,
                    color: AppColors.textSecondary,
                  ),
                  SizedBox(width: AppSpacing.xs),
                  Text(
                    'TYPOGRAPHY',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      letterSpacing: 0.5,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
              const SizedBox(width: AppSpacing.sm),
              Flexible(
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 2,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.brandPrimaryContainer,
                    borderRadius: AppRadius.smBorder,
                    border: Border.all(color: AppColors.infoBorder),
                  ),
                  child: Text(
                    effectiveFamily,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      color: AppColors.brandPrimary,
                    ),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Text('Purch.io Point of Sale', style: headerStyle),
          const SizedBox(height: 2),
          Text(
            'Fast touch-optimized checkout for modern Philippine retail.',
            style: bodyStyle,
          ),
          const SizedBox(height: AppSpacing.sm),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: AppRadius.smBorder,
              border: Border.all(color: AppColors.border),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text('1× Iced Spanish Latte (16oz)', style: bodyStyle),
                ),
                Text('₱165.00', style: priceStyle),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

