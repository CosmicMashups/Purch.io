import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../onboarding/presentation/screens/bootstrap_screen.dart';
import '../../../onboarding/presentation/screens/server_connection_screen.dart';
import '../../domain/auth_models.dart';
import '../providers/auth_providers.dart';

/// Where a person signs in with their email and password. A person in several
/// businesses is then asked which one to open. A device is paired from here too
/// (the "Pair this device" link), and a brand-new business is set up from here.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key, required this.onLoggedIn});

  /// Called once login succeeds.
  final VoidCallback onLoggedIn;

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit({String? tenantId}) async {
    if (!(_formKey.currentState?.validate() ?? false) && tenantId == null) {
      return;
    }

    final controller = ref.read(signInControllerProvider.notifier);
    final signedIn = await controller.signIn(
      email: _emailController.text.trim(),
      password: _passwordController.text,
      tenantId: tenantId,
    );

    if (!mounted) {
      return;
    }

    if (signedIn) {
      widget.onLoggedIn();
    }
  }

  @override
  Widget build(BuildContext context) {
    final loginState = ref.watch(signInControllerProvider);
    final isLoading = loginState.isLoading;
    final failure = ref.read(signInControllerProvider.notifier).currentFailure;
    final businesses = loginState.valueOrNull;

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
                                  emailController: _emailController,
                                  passwordController: _passwordController,
                                  isLoading: isLoading,
                                  failure: failure,
                                  businesses: businesses,
                                  onSubmit: _submit,
                                  onChooseBusiness:
                                      (tenantId) => _submit(tenantId: tenantId),
                                  onPairDevice: () => context.go('/pair'),
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
                padding: const EdgeInsets.symmetric(
                  horizontal: 20,
                  vertical: 16,
                ),
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
                          emailController: _emailController,
                          passwordController: _passwordController,
                          isLoading: isLoading,
                          failure: failure,
                          businesses: businesses,
                          onSubmit: _submit,
                          onChooseBusiness:
                              (tenantId) => _submit(tenantId: tenantId),
                          onPairDevice: () => context.go('/pair'),
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
        Image.asset('assets/images/login_hero.jpg', fit: BoxFit.cover),
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
                      child: Image.asset('assets/logo.jpg', fit: BoxFit.cover),
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
                    child: Image.asset('assets/logo.jpg', fit: BoxFit.cover),
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
    required this.emailController,
    required this.passwordController,
    required this.isLoading,
    required this.failure,
    required this.businesses,
    required this.onSubmit,
    required this.onChooseBusiness,
    required this.onPairDevice,
    required this.onBootstrap,
    this.showBottomLinks = true,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController emailController;
  final TextEditingController passwordController;
  final bool isLoading;
  final Failure? failure;

  /// Non-null once the person's email belongs to several businesses.
  final List<BusinessChoice>? businesses;
  final VoidCallback onSubmit;
  final ValueChanged<String> onChooseBusiness;
  final VoidCallback onPairDevice;
  final VoidCallback onBootstrap;
  final bool showBottomLinks;

  @override
  Widget build(BuildContext context) {
    final choices = businesses;

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
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Sign in',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                        letterSpacing: -0.2,
                      ),
                    ),
                    Text(
                      choices == null
                          ? 'Use your email and password'
                          : 'Which business do you want to open?',
                      style: const TextStyle(
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

          if (choices != null) ...[
            for (final business in choices)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: OutlinedButton(
                  onPressed:
                      isLoading
                          ? null
                          : () => onChooseBusiness(business.tenantId),
                  child: Text(business.name),
                ),
              ),
          ] else ...[
            TextFormField(
              controller: emailController,
              enabled: !isLoading,
              decoration: const InputDecoration(
                labelText: 'Email',
                prefixIcon: Icon(Icons.email_outlined, size: 20),
              ),
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              autofillHints: const [AutofillHints.email],
              validator:
                  (value) =>
                      (value == null || value.trim().isEmpty)
                          ? 'Required'
                          : null,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: passwordController,
              enabled: !isLoading,
              obscureText: true,
              decoration: const InputDecoration(
                labelText: 'Password',
                prefixIcon: Icon(Icons.lock_outline_rounded, size: 20),
              ),
              textInputAction: TextInputAction.done,
              autofillHints: const [AutofillHints.password],
              onFieldSubmitted: (_) => isLoading ? null : onSubmit(),
              validator:
                  (value) =>
                      (value == null || value.isEmpty) ? 'Required' : null,
            ),
          ],

          if (failure != null) ...[
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.errorContainer,
                borderRadius: AppRadius.smBorder,
              ),
              child: Text(
                failure!.message,
                style: const TextStyle(
                  fontSize: 13,
                  color: AppColors.error,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ],

          if (choices == null) ...[
            const SizedBox(height: 20),
            FilledButton(
              onPressed: isLoading ? null : onSubmit,
              child:
                  isLoading
                      ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                      : const Text('Sign in'),
            ),
          ],

          const SizedBox(height: 12),
          TextButton.icon(
            onPressed: isLoading ? null : onPairDevice,
            icon: const Icon(Icons.qr_code_2_rounded, size: 18),
            label: const Text('Pair this device with a code'),
          ),

          if (showBottomLinks) ...[
            const SizedBox(height: 4),
            Wrap(
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
                    padding: EdgeInsets.symmetric(horizontal: 4, vertical: 2),
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
          ],
        ],
      ),
    );
  }
}
