import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../kiosk/presentation/screens/kiosk_pairing_screen.dart';
import '../../../onboarding/presentation/screens/bootstrap_screen.dart';
import '../../../onboarding/presentation/screens/server_connection_screen.dart';
import '../providers/auth_providers.dart';
import 'admin_login_screen.dart';

/// The first screen any staff member sees on a paired device.
/// Hero banner establishes the brand moment (logo, name, tagline) on a
/// gradient field; the card below stays a focused, tactile pairing-code +
/// PIN form — Purch.io logs staff into a device, not an email account.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key, required this.onLoggedIn});

  /// Called once login succeeds. Kept as a callback rather than baking in
  /// go_router navigation here, since the full route tree isn't built yet —
  /// this screen shouldn't need to change when it is.
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
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  _HeroBanner(),
                  Transform.translate(
                    offset: const Offset(0, -28),
                    child: Container(
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: AppRadius.xlBorder,
                        boxShadow: AppShadows.card,
                        border: Border.all(color: AppColors.border),
                      ),
                      padding: const EdgeInsets.all(32),
                      child: Form(
                        key: _formKey,
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
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
                                    Icons.login_rounded,
                                    color: AppColors.brandPrimary,
                                    size: 22,
                                  ),
                                ),
                                const SizedBox(width: 12),
                                const Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        'Welcome Back',
                                        style: TextStyle(
                                          fontSize: 20,
                                          fontWeight: FontWeight.w800,
                                          color: AppColors.textPrimary,
                                        ),
                                      ),
                                      Text(
                                        'Log in to this device to continue',
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
                            const SizedBox(height: 24),

                            // Device pairing code
                            TextFormField(
                              controller: _pairingCodeController,
                              enabled: !isLoading,
                              decoration: const InputDecoration(
                                labelText: 'Device pairing code',
                                prefixIcon: Icon(
                                  Icons.tablet_mac_rounded,
                                  size: 20,
                                ),
                              ),
                              textInputAction: TextInputAction.next,
                              validator:
                                  (value) =>
                                      (value == null || value.trim().isEmpty)
                                          ? 'Required'
                                          : null,
                            ),
                            const SizedBox(height: 16),

                            // PIN
                            TextFormField(
                              controller: _pinController,
                              enabled: !isLoading,
                              decoration: const InputDecoration(
                                labelText: 'PIN',
                                prefixIcon: Icon(
                                  Icons.lock_outline_rounded,
                                  size: 20,
                                ),
                              ),
                              keyboardType: TextInputType.number,
                              obscureText: true,
                              textInputAction: TextInputAction.done,
                              onFieldSubmitted: (_) => _submit(),
                              validator:
                                  (value) =>
                                      (value == null || value.trim().isEmpty)
                                          ? 'Required'
                                          : null,
                            ),

                            // Failure notice
                            if (failure != null) ...[
                              const SizedBox(height: 16),
                              Container(
                                padding: const EdgeInsets.all(12),
                                decoration: BoxDecoration(
                                  color: AppColors.error.withAlpha(20),
                                  borderRadius: AppRadius.smBorder,
                                  border: Border.all(
                                    color: AppColors.error.withAlpha(60),
                                  ),
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
                                        failure.message,
                                        style: const TextStyle(
                                          fontSize: 13,
                                          color: AppColors.error,
                                          fontWeight: FontWeight.w500,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],

                            const SizedBox(height: 24),

                            // Primary Login Button
                            SizedBox(
                              height: 52,
                              child: FilledButton(
                                onPressed: isLoading ? null : _submit,
                                style: FilledButton.styleFrom(
                                  backgroundColor: AppColors.brandPrimary,
                                  foregroundColor: AppColors.onBrandPrimary,
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
                                            strokeWidth: 2.5,
                                            color: AppColors.onBrandPrimary,
                                          ),
                                        )
                                        : const Text(
                                          'Log In',
                                          style: TextStyle(
                                            fontSize: 16,
                                            fontWeight: FontWeight.w600,
                                          ),
                                        ),
                              ),
                            ),

                            const SizedBox(height: 28),
                            const Divider(color: AppColors.border, height: 1),
                            const SizedBox(height: 16),

                            // Secondary Navigation Links
                            TextButton.icon(
                              icon: const Icon(
                                Icons.store_mall_directory_outlined,
                                size: 18,
                                color: AppColors.brandPrimary,
                              ),
                              label: const Text(
                                'Set up a new business',
                                style: TextStyle(
                                  color: AppColors.brandPrimary,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              onPressed:
                                  isLoading
                                      ? null
                                      : () => Navigator.of(context).push<void>(
                                        MaterialPageRoute(
                                          builder:
                                              (_) => const BootstrapScreen(),
                                        ),
                                      ),
                            ),
                            TextButton.icon(
                              icon: const Icon(
                                Icons.touch_app_outlined,
                                size: 18,
                                color: AppColors.textSecondary,
                              ),
                              label: const Text(
                                'Set up as a self-order kiosk',
                                style: TextStyle(
                                  color: AppColors.textSecondary,
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                              onPressed:
                                  isLoading
                                      ? null
                                      : () => Navigator.of(context).push<void>(
                                        MaterialPageRoute(
                                          builder:
                                              (_) => KioskPairingScreen(
                                                onPaired: widget.onLoggedIn,
                                              ),
                                        ),
                                      ),
                            ),
                            TextButton.icon(
                              icon: const Icon(
                                Icons.dns_outlined,
                                size: 18,
                                color: AppColors.textSecondary,
                              ),
                              label: const Text(
                                'Connect to a local server',
                                style: TextStyle(
                                  color: AppColors.textSecondary,
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                              onPressed:
                                  isLoading
                                      ? null
                                      : () => Navigator.of(context).push<void>(
                                        MaterialPageRoute(
                                          builder:
                                              (_) =>
                                                  const ServerConnectionScreen(),
                                        ),
                                      ),
                            ),
                            TextButton(
                              onPressed:
                                  isLoading
                                      ? null
                                      : () => Navigator.of(context).push<void>(
                                        MaterialPageRoute(
                                          builder:
                                              (_) => AdminLoginScreen(
                                                onLoggedIn: widget.onLoggedIn,
                                              ),
                                        ),
                                      ),
                              child: const Text(
                                'Sign in as admin instead',
                                style: TextStyle(
                                  color: AppColors.textSecondary,
                                  fontWeight: FontWeight.w500,
                                  fontSize: 13,
                                ),
                              ),
                            ),
                          ],
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
}

/// Full-bleed photographic banner establishing the brand moment above the
/// login card. `assets/images/login_hero.jpg` is a deliberately
/// vertical-agnostic storefront scene (blurred cues from café, grocery,
/// and retail all at once, no single business type dominant) so it reads
/// as "a Filipino small business" regardless of which vertical a given
/// tenant runs. A brand-ultramarine gradient scrim sits over it, both for
/// text legibility and to keep the banner tied to the app's own palette
/// rather than looking like a stock photo pasted on top.
class _HeroBanner extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: const BorderRadius.vertical(top: Radius.circular(28)),
      child: SizedBox(
        height: 320,
        child: Stack(
          fit: StackFit.expand,
          children: [
            Image.asset(
              'assets/images/login_hero.jpg',
              fit: BoxFit.cover,
              alignment: Alignment.topCenter,
            ),
            // Ultramarine scrim: heavier at top (logo sits there) and at the
            // bottom edge (where it meets the white card), lighter through
            // the middle so the photo still reads as a real place.
            DecoratedBox(
              decoration: const BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [
                    Color(0xCC1E3A8A),
                    Color(0x661E40AF),
                    Color(0xE61E3A8A),
                  ],
                  stops: [0.0, 0.55, 1.0],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(
                vertical: 32,
                horizontal: 24,
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  Container(
                    width: 84,
                    height: 84,
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: AppRadius.xlBorder,
                      boxShadow: const [
                        BoxShadow(
                          color: Color(0x40000000),
                          blurRadius: 16,
                          offset: Offset(0, 8),
                        ),
                      ],
                    ),
                    padding: const EdgeInsets.all(13),
                    child: ClipRRect(
                      borderRadius: AppRadius.mdBorder,
                      child: Image.asset('assets/logo.jpg', fit: BoxFit.cover),
                    ),
                  ),
                  const SizedBox(height: 18),
                  Image.asset(
                    'assets/wordmark.png',
                    height: 32,
                    color: Colors.white,
                    colorBlendMode: BlendMode.srcIn,
                    semanticLabel: 'Purch.io',
                  ),
                  const SizedBox(height: 10),
                  const Text(
                    'One POS core for every kind of business',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                      letterSpacing: 0.1,
                      shadows: [
                        Shadow(color: Color(0x80000000), blurRadius: 6),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
