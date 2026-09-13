import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/purch_image.dart';
import '../providers/kiosk_providers.dart';
import 'kiosk_category_screen.dart';

/// E1 - The kiosk idle/landing screen.
/// Customer-facing portrait view. Tactile, warm, inviting retail hero.
/// When the tenant has uploaded a poster URL it is shown as a full-width
/// 16:9 hero card; otherwise the branded wordmark card is the fallback.
class KioskLandingScreen extends ConsumerStatefulWidget {
  const KioskLandingScreen({super.key});

  @override
  ConsumerState<KioskLandingScreen> createState() =>
      _KioskLandingScreenState();
}

class _KioskLandingScreenState extends ConsumerState<KioskLandingScreen>
    with SingleTickerProviderStateMixin {
  late AnimationController _pulseController;
  late Animation<double> _pulseScale;

  @override
  void initState() {
    super.initState();
    unawaited(
      SystemChrome.setPreferredOrientations([
        DeviceOrientation.portraitUp,
        DeviceOrientation.portraitDown,
      ]),
    );

    _pulseController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1400),
    )..repeat(reverse: true);

    _pulseScale = Tween<double>(begin: 1.0, end: 1.025).animate(
      CurvedAnimation(parent: _pulseController, curve: Curves.easeInOut),
    );
  }

  @override
  void dispose() {
    _pulseController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final brandingAsync = ref.watch(kioskBrandingProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 500),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  // Top: self-service badge
                  Column(
                    children: [
                      const SizedBox(height: 24),
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 16,
                          vertical: 6,
                        ),
                        decoration: BoxDecoration(
                          color: AppColors.brandPrimaryContainer,
                          borderRadius: BorderRadius.circular(AppRadius.full),
                          border: Border.all(
                            color: AppColors.brandPrimary.withAlpha(40),
                          ),
                        ),
                        child: const Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(
                              Icons.touch_app_rounded,
                              size: 16,
                              color: AppColors.brandPrimary,
                            ),
                            SizedBox(width: 6),
                            Text(
                              'SELF-SERVICE ORDER KIOSK',
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w700,
                                letterSpacing: 1.2,
                                color: AppColors.brandPrimary,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),

                  // Center hero
                  Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      brandingAsync.when(
                        loading: _buildHeroShimmer,
                        error: (_, __) => _buildPosterCard(null),
                        data: (branding) =>
                            _buildPosterCard(branding.kioskPosterImageUrl),
                      ),
                      const SizedBox(height: 36),
                      const Text(
                        'Welcome!',
                        style: TextStyle(
                          fontSize: 34,
                          fontWeight: FontWeight.w800,
                          color: AppColors.textPrimary,
                          letterSpacing: -0.6,
                        ),
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 10),
                      const Text(
                        'Tap below to start your order.',
                        style: TextStyle(
                          fontSize: 18,
                          color: AppColors.textSecondary,
                          fontWeight: FontWeight.w400,
                        ),
                        textAlign: TextAlign.center,
                      ),
                    ],
                  ),

                  // Bottom CTA
                  Padding(
                    padding: const EdgeInsets.only(bottom: 24),
                    child: AnimatedBuilder(
                      animation: _pulseScale,
                      builder: (context, child) {
                        return Transform.scale(
                          scale: _pulseScale.value,
                          child: child,
                        );
                      },
                      child: Container(
                        decoration: const BoxDecoration(
                          boxShadow: AppShadows.tactileButton,
                          borderRadius: BorderRadius.all(
                            Radius.circular(AppRadius.lg),
                          ),
                        ),
                        child: SizedBox(
                          height: 72,
                          width: double.infinity,
                          child: FilledButton(
                            style: FilledButton.styleFrom(
                              backgroundColor: AppColors.brandPrimary,
                              foregroundColor: AppColors.onBrandPrimary,
                              shape: const RoundedRectangleBorder(
                                borderRadius: BorderRadius.all(
                                  Radius.circular(AppRadius.lg),
                                ),
                              ),
                            ),
                            onPressed:
                                () => Navigator.of(context).push<void>(
                                  MaterialPageRoute(
                                    builder: (_) =>
                                        const KioskCategoryScreen(),
                                  ),
                                ),
                            child: const Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(Icons.restaurant_menu_rounded, size: 28),
                                SizedBox(width: 12),
                                Text(
                                  'Start Order',
                                  style: TextStyle(
                                    fontSize: 22,
                                    fontWeight: FontWeight.w700,
                                    letterSpacing: 0.2,
                                  ),
                                ),
                              ],
                            ),
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

  // Hero widget variants

  /// Full-bleed 16:9 poster image card shown with the tenant's poster, falling back
  /// to the taste-crafted default universal retail poster or branded wordmark.
  Widget _buildPosterCard(String? url) {
    return ClipRRect(
      borderRadius: AppRadius.lgBorder,
      child: AspectRatio(
        aspectRatio: 16 / 9,
        child: PurchImage(
          imageUrlOrPath: url,
          fallbackAsset: 'assets/images/kiosk_poster_default.jpg',
          fit: BoxFit.cover,
          semanticLabel: 'Promotional poster',
          errorWidget: _buildWordmarkCard(),
        ),
      ),
    );
  }

  /// Branded wordmark card - default state when no poster URL is configured.
  Widget _buildWordmarkCard() {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.xlBorder,
        boxShadow: AppShadows.card,
        border: Border.all(color: AppColors.border),
      ),
      child: Image.asset(
        'assets/wordmark.png',
        height: 84,
        fit: BoxFit.contain,
        semanticLabel: 'Purch.io',
      ),
    );
  }

  /// Shimmer-shaped placeholder while the poster URL is being fetched.
  Widget _buildHeroShimmer() {
    return ClipRRect(
      borderRadius: AppRadius.lgBorder,
      child: AspectRatio(
        aspectRatio: 16 / 9,
        child: Container(color: AppColors.cardHover),
      ),
    );
  }
}
