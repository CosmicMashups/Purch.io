import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../onboarding/presentation/screens/bootstrap_screen.dart';
import '../../../onboarding/presentation/screens/server_connection_screen.dart';
import '../providers/auth_providers.dart';
import 'admin_login_screen.dart';

/// The first screen any staff member sees on a paired device.
/// Designed for fast, distraction-free terminal sign-in:
/// - Compact brand header with direct access to local server network settings.
/// - Focused tactile card for Device Pairing Code + PIN and prominent Log In action.
/// - Secondary "Sign in as admin instead" for store owners.
/// - Footer link for first-time business onboarding.
/// - Completely fits above the fold in standard 9:16 mobile and tablet viewports.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key, required this.onLoggedIn});

  /// Called once login succeeds.
  final VoidCallback onLoggedIn;

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _pairingCodeController = TextEditingController();
  final _pinController = TextEditingController();

  @override
  void dispose() {
    _pairingCodeController.dispose();
    _pinController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(loginControllerProvider.notifier);
    await controller.login(
      devicePairingCode: _pairingCodeController.text.trim(),
      pin: _pinController.text.trim(),
    );

    if (!mounted) {
      return;
    }

    final succeeded = !ref.read(loginControllerProvider).hasError;
    if (succeeded) {
      widget.onLoggedIn();
    }
  }

  @override
  Widget build(BuildContext context) {
    final loginState = ref.watch(loginControllerProvider);
    final isLoading = loginState.isLoading;
    final failure = ref.read(loginControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) {
            final isWide = constraints.maxWidth >= 768;

            if (isWide) {
              return Center(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(24),
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 960),
                    child: Container(
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: AppRadius.lgBorder,
                        border: Border.all(color: AppColors.border),
                        boxShadow: AppShadows.card,
                      ),
                      clipBehavior: Clip.antiAlias,
                      child: IntrinsicHeight(
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            // Left Hero Pane
                            Expanded(
                              flex: 5,
                              child: _HeroBannerPane(isLoading: isLoading),
                            ),
                            // Vertical Divider
                            Container(width: 1, color: AppColors.border),
                            // Right Form Pane
                            Expanded(
                              flex: 5,
                              child: Padding(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 36,
                                  vertical: 36,
                                ),
                                child: _LoginFormCard(
                                  formKey: _formKey,
                                  pairingCodeController: _pairingCodeController,
                                  pinController: _pinController,
                                  isLoading: isLoading,
                                  failure: failure,
                                  onSubmit: _submit,
                                  onAdminLogin:
                                      () => Navigator.of(context).push<void>(
                                        MaterialPageRoute(
                                          builder:
                                              (_) => AdminLoginScreen(
                                                onLoggedIn: widget.onLoggedIn,
                                              ),
                                        ),
                                      ),
                                  onBootstrap:
                                      () => Navigator.of(context).push<void>(
                                        MaterialPageRoute(
                                          builder:
                                              (_) => const BootstrapScreen(),
                                        ),
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
              );
            }

            // Compact View (e.g. mobile 360x640)
            return Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 420),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      // Top compact hero & brand header
                      _CompactHeroHeader(isLoading: isLoading),
                      const SizedBox(height: 16),

                      // Sign-in card
                      Container(
                        decoration: BoxDecoration(
                          color: AppColors.surface,
                          borderRadius: AppRadius.lgBorder,
                          border: Border.all(color: AppColors.border),
                          boxShadow: AppShadows.card,
                        ),
                        padding: const EdgeInsets.all(24),
                        child: _LoginFormCard(
                          formKey: _formKey,
                          pairingCodeController: _pairingCodeController,
                          pinController: _pinController,
                          isLoading: isLoading,
                          failure: failure,
                          onSubmit: _submit,
                          onAdminLogin:
                              () => Navigator.of(context).push<void>(
                                MaterialPageRoute(
                                  builder:
                                      (_) => AdminLoginScreen(
                                        onLoggedIn: widget.onLoggedIn,
                                      ),
                                ),
                              ),
                          onBootstrap:
                              () => Navigator.of(context).push<void>(
                                MaterialPageRoute(
                                  builder: (_) => const BootstrapScreen(),
                                ),
                              ),
                          showBottomLinks: false,
                        ),
                      ),
                      const SizedBox(height: 16),

                      // Bottom Onboarding Link
                      Center(
                        child: Wrap(
                          alignment: WrapAlignment.center,
                          crossAxisAlignment: WrapCrossAlignment.center,
                          children: [
                            const Text(
                              'New to Purch.io? ',
                              style: TextStyle(
                                fontSize: 13,
                                color: AppColors.textSecondary,
                              ),
                            ),
                            InkWell(
                              onTap:
                                  isLoading
                                      ? null
                                      : () => Navigator.of(context).push<void>(
                                        MaterialPageRoute(
                                          builder:
                                              (_) => const BootstrapScreen(),
                                        ),
                                      ),
                              borderRadius: AppRadius.smBorder,
                              child: const Padding(
                                padding: EdgeInsets.symmetric(
                                  horizontal: 4,
                                  vertical: 2,
                                ),
                                child: Text(
                                  'Set up a new business',
                                  style: TextStyle(
                                    fontSize: 13,
                                    color: AppColors.brandPrimary,
                                    fontWeight: FontWeight.w700,
                                  ),
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
            );
          },
        ),
      ),
    );
  }
}

/// Rich wide-screen hero pane with dark gradient overlay, wordmark, and merchant tagline.
class _HeroBannerPane extends StatelessWidget {
  const _HeroBannerPane({required this.isLoading});

  final bool isLoading;

  @override
  Widget build(BuildContext context) {
    return Stack(
      fit: StackFit.expand,
      children: [
        // Hero Photo
        Image.asset(
          'assets/images/login_hero.jpg',
          fit: BoxFit.cover,
        ),
        // Tint & Vignette Gradient
        DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [
                const Color(0xD90A2E2B),
                const Color(0x990F766E),
                const Color(0xF0071E1C),
              ],
              stops: const [0.0, 0.45, 1.0],
            ),
          ),
        ),
        // Content Overlay
        Padding(
          padding: const EdgeInsets.all(28),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              // Top Bar: Logo + Wordmark + Server Link
              Row(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  Container(
                    width: 40,
                    height: 40,
                    decoration: const BoxDecoration(
                      color: Colors.white,
                      borderRadius: AppRadius.mdBorder,
                    ),
                    padding: const EdgeInsets.all(6),
                    child: ClipRRect(
                      borderRadius: AppRadius.smBorder,
                      child: Image.asset(
                        'assets/logo.jpg',
                        fit: BoxFit.cover,
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Image.asset(
                    'assets/wordmark.png',
                    height: 22,
                    color: Colors.white,
                    colorBlendMode: BlendMode.srcIn,
                    semanticLabel: 'Purch.io',
                  ),
                  const Spacer(),
                  IconButton(
                    tooltip: 'Connect to a local server',
                    icon: const Icon(
                      Icons.dns_outlined,
                      color: Colors.white,
                      size: 20,
                    ),
                    style: IconButton.styleFrom(
                      backgroundColor: const Color(0x33FFFFFF),
                      visualDensity: VisualDensity.compact,
                    ),
                    onPressed:
                        isLoading
                            ? null
                            : () => Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder: (_) => const ServerConnectionScreen(),
                              ),
                            ),
                  ),
                ],
              ),

              // Bottom Accent: Operating Headline
              const Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'One POS core for every kind of business.',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 22,
                      fontWeight: FontWeight.w700,
                      letterSpacing: -0.3,
                      height: 1.25,
                    ),
                  ),
                  SizedBox(height: 6),
                  Text(
                    'Fast, offline-resilient register checkout designed for high-volume retail, dining, and multi-location counters.',
                    style: TextStyle(
                      color: Color(0xCCFFFFFF),
                      fontSize: 12,
                      height: 1.45,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Compact header for mobile viewports (< 768px) with subtle hero backdrop.
class _CompactHeroHeader extends StatelessWidget {
  const _CompactHeroHeader({required this.isLoading});

  final bool isLoading;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.brandPrimary,
        borderRadius: AppRadius.lgBorder,
        boxShadow: const [
          BoxShadow(
            color: Color(0x200F766E),
            blurRadius: 12,
            offset: Offset(0, 4),
          ),
        ],
      ),
      clipBehavior: Clip.antiAlias,
      child: Stack(
        children: [
          // Background Hero Texture
          Positioned.fill(
            child: Opacity(
              opacity: 0.22,
              child: Image.asset(
                'assets/images/login_hero.jpg',
                fit: BoxFit.cover,
                alignment: Alignment.centerRight,
              ),
            ),
          ),
          // Content
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            child: Row(
              children: [
                // Logo
                Container(
                  width: 44,
                  height: 44,
                  decoration: const BoxDecoration(
                    color: Colors.white,
                    borderRadius: AppRadius.mdBorder,
                  ),
                  padding: const EdgeInsets.all(7),
                  child: ClipRRect(
                    borderRadius: AppRadius.smBorder,
                    child: Image.asset(
                      'assets/logo.jpg',
                      fit: BoxFit.cover,
                    ),
                  ),
                ),
                const SizedBox(width: 12),

                // Brand Wordmark + Tagline
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Image.asset(
                        'assets/wordmark.png',
                        height: 22,
                        color: Colors.white,
                        colorBlendMode: BlendMode.srcIn,
                        semanticLabel: 'Purch.io',
                      ),
                      const SizedBox(height: 2),
                      const Text(
                        'One POS core for every kind of business',
                        style: TextStyle(
                          color: Color(0xCCFFFFFF),
                          fontSize: 11,
                          fontWeight: FontWeight.w400,
                          letterSpacing: 0.1,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ),
                ),

                // Server Connection Action
                IconButton(
                  tooltip: 'Connect to a local server',
                  icon: const Icon(
                    Icons.dns_outlined,
                    color: Colors.white,
                    size: 20,
                  ),
                  style: IconButton.styleFrom(
                    backgroundColor: const Color(0x26FFFFFF),
                    visualDensity: VisualDensity.compact,
                  ),
                  onPressed:
                      isLoading
                          ? null
                          : () => Navigator.of(context).push<void>(
                            MaterialPageRoute(
                              builder: (_) => const ServerConnectionScreen(),
                            ),
                          ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Sign-in form card shared between split and compact viewports.
class _LoginFormCard extends StatelessWidget {
  const _LoginFormCard({
    required this.formKey,
    required this.pairingCodeController,
    required this.pinController,
    required this.isLoading,
    required this.failure,
    required this.onSubmit,
    required this.onAdminLogin,
    required this.onBootstrap,
    this.showBottomLinks = true,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController pairingCodeController;
  final TextEditingController pinController;
  final bool isLoading;
  final Failure? failure;
  final VoidCallback onSubmit;
  final VoidCallback onAdminLogin;
  final VoidCallback onBootstrap;
  final bool showBottomLinks;

  @override
  Widget build(BuildContext context) {
    return Form(
      key: formKey,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: AppColors.brandPrimaryContainer,
                  borderRadius: AppRadius.mdBorder,
                ),
                child: const Icon(
                  Icons.login_rounded,
                  color: AppColors.brandPrimary,
                  size: 20,
                ),
              ),
              const SizedBox(width: 12),
              const Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Device Sign In',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                        letterSpacing: -0.2,
                      ),
                    ),
                    Text(
                      'Enter terminal pairing code and PIN',
                      style: TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),

          // Device pairing code
          TextFormField(
            controller: pairingCodeController,
            enabled: !isLoading,
            decoration: InputDecoration(
              labelText: 'Device pairing code',
              hintText: 'e.g. POS-01-REG',
              prefixIcon: const Icon(
                Icons.tablet_mac_rounded,
                size: 20,
                color: AppColors.textSecondary,
              ),
              filled: true,
              fillColor: AppColors.background,
              contentPadding: const EdgeInsets.symmetric(
                horizontal: 16,
                vertical: 14,
              ),
              border: OutlineInputBorder(
                borderRadius: AppRadius.mdBorder,
                borderSide: const BorderSide(color: AppColors.border),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: AppRadius.mdBorder,
                borderSide: const BorderSide(color: AppColors.border),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: AppRadius.mdBorder,
                borderSide: const BorderSide(
                  color: AppColors.brandPrimary,
                  width: 1.5,
                ),
              ),
            ),
            textInputAction: TextInputAction.next,
            validator:
                (value) =>
                    (value == null || value.trim().isEmpty) ? 'Required' : null,
          ),
          const SizedBox(height: 14),

          // PIN field
          TextFormField(
            controller: pinController,
            enabled: !isLoading,
            decoration: InputDecoration(
              labelText: 'PIN',
              hintText: '4-digit staff PIN',
              prefixIcon: const Icon(
                Icons.lock_outline_rounded,
                size: 20,
                color: AppColors.textSecondary,
              ),
              filled: true,
              fillColor: AppColors.background,
              contentPadding: const EdgeInsets.symmetric(
                horizontal: 16,
                vertical: 14,
              ),
              border: OutlineInputBorder(
                borderRadius: AppRadius.mdBorder,
                borderSide: const BorderSide(color: AppColors.border),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: AppRadius.mdBorder,
                borderSide: const BorderSide(color: AppColors.border),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: AppRadius.mdBorder,
                borderSide: const BorderSide(
                  color: AppColors.brandPrimary,
                  width: 1.5,
                ),
              ),
            ),
            keyboardType: TextInputType.number,
            obscureText: true,
            textInputAction: TextInputAction.done,
            onFieldSubmitted: (_) => onSubmit(),
            validator:
                (value) =>
                    (value == null || value.trim().isEmpty) ? 'Required' : null,
          ),

          // Failure banner
          if (failure != null) ...[
            const SizedBox(height: 14),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              decoration: BoxDecoration(
                color: AppColors.errorContainer,
                borderRadius: AppRadius.smBorder,
                border: Border.all(color: AppColors.errorBorder),
              ),
              child: Row(
                children: [
                  const Icon(
                    Icons.error_outline_rounded,
                    size: 18,
                    color: AppColors.error,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      failure!.message,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.onErrorContainer,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],

          const SizedBox(height: 18),

          // Primary Log In Button
          SizedBox(
            height: 48,
            child: FilledButton(
              onPressed: isLoading ? null : onSubmit,
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.brandPrimary,
                foregroundColor: AppColors.onBrandPrimary,
                elevation: 0,
                shape: const RoundedRectangleBorder(
                  borderRadius: AppRadius.mdBorder,
                ),
              ),
              child:
                  isLoading
                      ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2.2,
                          color: AppColors.onBrandPrimary,
                        ),
                      )
                      : const Text(
                        'Log In',
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
            ),
          ),

          const SizedBox(height: 12),

          // Secondary Admin Sign In
          Center(
            child: TextButton.icon(
              onPressed: isLoading ? null : onAdminLogin,
              icon: const Icon(
                Icons.admin_panel_settings_outlined,
                size: 16,
                color: AppColors.textSecondary,
              ),
              label: const Text(
                'Sign in as admin instead',
                style: TextStyle(
                  color: AppColors.textSecondary,
                  fontWeight: FontWeight.w500,
                  fontSize: 13,
                ),
              ),
              style: TextButton.styleFrom(
                visualDensity: VisualDensity.compact,
                padding: const EdgeInsets.symmetric(
                  horizontal: 12,
                  vertical: 8,
                ),
              ),
            ),
          ),

          // In wide split mode, show onboarding setup within the card bottom
          if (showBottomLinks) ...[
            const SizedBox(height: 12),
            const Divider(color: AppColors.border, height: 1),
            const SizedBox(height: 12),
            Center(
              child: Wrap(
                alignment: WrapAlignment.center,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  const Text(
                    'New to Purch.io? ',
                    style: TextStyle(
                      fontSize: 13,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  InkWell(
                    onTap: isLoading ? null : onBootstrap,
                    borderRadius: AppRadius.smBorder,
                    child: const Padding(
                      padding: EdgeInsets.symmetric(
                        horizontal: 4,
                        vertical: 2,
                      ),
                      child: Text(
                        'Set up a new business',
                        style: TextStyle(
                          fontSize: 13,
                          color: AppColors.brandPrimary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

