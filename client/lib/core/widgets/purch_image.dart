import 'package:flutter/material.dart';

import '../config/app_config.dart';
import '../theming/app_tokens.dart';

/// Renders an image from either:
/// 1. Local bundled Flutter assets (`assets/...`)
/// 2. Dynamic tenant backend uploads (`/uploads/...`)
/// 3. Remote external URLs (`http://`, `https://`)
///
/// Provides graceful fallback handling, shimmering placeholders,
/// and rounded corner clipping.
class PurchImage extends StatelessWidget {
  const PurchImage({
    super.key,
    required this.imageUrlOrPath,
    this.width,
    this.height,
    this.fit = BoxFit.cover,
    this.borderRadius,
    this.fallbackAsset,
    this.semanticLabel,
    this.errorWidget,
  });

  final String? imageUrlOrPath;
  final double? width;
  final double? height;
  final BoxFit fit;
  final BorderRadius? borderRadius;
  final String? fallbackAsset;
  final String? semanticLabel;
  final Widget? errorWidget;

  /// Resolves relative paths like `/uploads/...` to full URLs using [AppConfig.apiBaseUrl].
  static String resolveUrl(String pathOrUrl) {
    if (pathOrUrl.startsWith('http://') ||
        pathOrUrl.startsWith('https://') ||
        pathOrUrl.startsWith('assets/')) {
      return pathOrUrl;
    }
    final cleanBase = AppConfig.apiBaseUrl.endsWith('/')
        ? AppConfig.apiBaseUrl.substring(0, AppConfig.apiBaseUrl.length - 1)
        : AppConfig.apiBaseUrl;
    final cleanPath = pathOrUrl.startsWith('/') ? pathOrUrl : '/$pathOrUrl';
    return '$cleanBase$cleanPath';
  }

  @override
  Widget build(BuildContext context) {
    final effectiveSource = (imageUrlOrPath != null && imageUrlOrPath!.trim().isNotEmpty)
        ? imageUrlOrPath!.trim()
        : fallbackAsset;

    Widget imageContent;

    if (effectiveSource == null || effectiveSource.isEmpty) {
      imageContent = errorWidget ?? _defaultPlaceholder();
    } else if (effectiveSource.startsWith('assets/')) {
      imageContent = Image.asset(
        effectiveSource,
        width: width,
        height: height,
        fit: fit,
        semanticLabel: semanticLabel,
        errorBuilder: (context, error, stackTrace) =>
            errorWidget ?? _defaultPlaceholder(),
      );
    } else {
      final fullUrl = resolveUrl(effectiveSource);
      imageContent = Image.network(
        fullUrl,
        width: width,
        height: height,
        fit: fit,
        semanticLabel: semanticLabel,
        loadingBuilder: (context, child, loadingProgress) {
          if (loadingProgress == null) return child;
          return _defaultLoading();
        },
        errorBuilder: (context, error, stackTrace) {
          if (fallbackAsset != null && fallbackAsset!.isNotEmpty) {
            return Image.asset(
              fallbackAsset!,
              width: width,
              height: height,
              fit: fit,
              semanticLabel: semanticLabel,
              errorBuilder: (_, __, ___) =>
                  errorWidget ?? _defaultPlaceholder(),
            );
          }
          return errorWidget ?? _defaultPlaceholder();
        },
      );
    }

    if (borderRadius != null) {
      return ClipRRect(
        borderRadius: borderRadius!,
        child: imageContent,
      );
    }

    return imageContent;
  }

  Widget _defaultLoading() {
    return Container(
      width: width,
      height: height,
      color: AppColors.cardHover,
      child: const Center(
        child: SizedBox(
          width: 24,
          height: 24,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
      ),
    );
  }

  Widget _defaultPlaceholder() {
    return Container(
      width: width,
      height: height,
      color: AppColors.cardHover,
      child: const Center(
        child: Icon(
          Icons.image_outlined,
          color: AppColors.textMuted,
          size: 32,
        ),
      ),
    );
  }
}
