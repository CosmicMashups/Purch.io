import 'package:drift/drift.dart' show Value;
import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';
import 'package:purch_client/core/theming/app_theme.dart';
import 'package:purch_client/core/theming/app_tokens.dart';
import 'package:purch_client/core/theming/theme_builder.dart';

CachedBrandingData _branding({
  String? background,
  String? accent,
  String? primaryText,
  String? secondaryText,
}) {
  return CachedBrandingData(
    tenantId: 'tenant-1',
    logoUrl: null,
    backgroundColorHex: background,
    accentColorHex: accent,
    primaryTextColorHex: primaryText,
    secondaryTextColorHex: secondaryText,
    fontFamily: null,
    kioskPosterImageUrl: null,
    lastSyncedAt: DateTime.utc(2026, 1, 1),
  );
}

void main() {
  group('parseHexColor', () {
    test('parses #RRGGBB and bare RRGGBB as fully opaque', () {
      expect(parseHexColor('#123456'), const Color(0xFF123456));
      expect(parseHexColor('123456'), const Color(0xFF123456));
      expect(parseHexColor('  #abcdef  '), const Color(0xFFABCDEF));
    });

    test('parses #AARRGGBB', () {
      expect(parseHexColor('#80123456'), const Color(0x80123456));
    });

    test('returns null for null, blank and malformed input', () {
      expect(parseHexColor(null), isNull);
      expect(parseHexColor(''), isNull);
      expect(parseHexColor('   '), isNull);
      expect(parseHexColor('#12345'), isNull);
      expect(parseHexColor('nope'), isNull);
      expect(parseHexColor('#ZZZZZZ'), isNull);
      expect(parseHexColor('#12345678901234'), isNull);
    });
  });

  group('derivePrimaryContainer and deriveOnPrimaryContainer', () {
    test('derives luminous container and high-contrast foreground from default brand primary', () {
      final container = derivePrimaryContainer(AppColors.brandPrimary);
      final onContainer = deriveOnPrimaryContainer(AppColors.brandPrimary);

      final containerHsl = HSLColor.fromColor(container);
      final onContainerHsl = HSLColor.fromColor(onContainer);

      expect(containerHsl.lightness, closeTo(0.95, 0.01));
      expect(onContainerHsl.lightness, closeTo(0.20, 0.01));
    });

    test('derives harmonious containers across diverse hues without crashing', () {
      const hues = [
        Color(0xFF0F766E), // Pine Teal
        Color(0xFF9A3412), // Terracotta
        Color(0xFF831843), // Deep Wine
        Color(0xFF1E40AF), // Classic Blue
        Color(0xFF14532D), // Deep Forest Green
      ];

      for (final color in hues) {
        final container = derivePrimaryContainer(color);
        final onContainer = deriveOnPrimaryContainer(color);

        final containerHsl = HSLColor.fromColor(container);
        expect(containerHsl.lightness, closeTo(0.95, 0.01));

        final onContainerHsl = HSLColor.fromColor(onContainer);
        expect(onContainerHsl.lightness, closeTo(0.20, 0.01));
      }
    });
  });

  group('BrandingColors', () {
    test('null branding row resolves entirely to the AppColors defaults', () {
      final colors = BrandingColors.fromCachedBranding(null);

      expect(colors.background, AppColors.background);
      expect(colors.accent, AppColors.brandPrimary);
      expect(colors.primaryText, AppColors.textPrimary);
      expect(colors.secondaryText, AppColors.textSecondary);
      expect(colors, BrandingColors.defaults);
    });

    test('all four columns null resolves to the AppColors defaults', () {
      expect(
        BrandingColors.fromCachedBranding(_branding()),
        BrandingColors.defaults,
      );
    });

    test('a subset applies, the rest fall back', () {
      final colors = BrandingColors.fromCachedBranding(
        _branding(background: '#101010', primaryText: '#202020'),
      );

      expect(colors.background, const Color(0xFF101010));
      expect(colors.primaryText, const Color(0xFF202020));
      expect(colors.accent, AppColors.brandPrimary);
      expect(colors.secondaryText, AppColors.textSecondary);
    });

    test('invalid hex falls back instead of crashing', () {
      final colors = BrandingColors.fromCachedBranding(
        _branding(
          background: 'not-a-color',
          accent: '#GGGGGG',
          primaryText: '#1234',
          secondaryText: '#303030',
        ),
      );

      expect(colors.background, AppColors.background);
      expect(colors.accent, AppColors.brandPrimary);
      expect(colors.primaryText, AppColors.textPrimary);
      expect(colors.secondaryText, const Color(0xFF303030));
    });
  });

  group('built ThemeData', () {
    test('defaults match the unparameterised AppTheme themes', () {
      final staff = BrandingColors.defaults.toStaffTheme();
      final kiosk = BrandingColors.defaults.toKioskTheme();

      expect(staff.scaffoldBackgroundColor, AppTheme.staffTheme().scaffoldBackgroundColor);
      expect(staff.colorScheme.primary, AppTheme.staffTheme().colorScheme.primary);
      expect(staff.colorScheme.onSurface, AppTheme.staffTheme().colorScheme.onSurface);
      expect(kiosk.scaffoldBackgroundColor, AppTheme.kioskTheme().scaffoldBackgroundColor);
      expect(kiosk.colorScheme.primary, AppTheme.kioskTheme().colorScheme.primary);
    });

    test('defaults are the AppColors tokens', () {
      final staff = BrandingColors.defaults.toStaffTheme();

      expect(staff.scaffoldBackgroundColor, AppColors.background);
      expect(staff.colorScheme.primary, AppColors.brandPrimary);
      expect(staff.colorScheme.onSurface, AppColors.textPrimary);
      expect(staff.colorScheme.onSurfaceVariant, AppColors.textSecondary);
      expect(staff.appBarTheme.foregroundColor, AppColors.textPrimary);
    });

    test('configured colours reach the staff and kiosk themes', () {
      final colors = BrandingColors.fromCachedBranding(
        _branding(
          background: '#101010',
          accent: '#202020',
          primaryText: '#303030',
          secondaryText: '#404040',
        ),
      );

      for (final theme in [colors.toStaffTheme(), colors.toKioskTheme()]) {
        expect(theme.scaffoldBackgroundColor, const Color(0xFF101010));
        expect(theme.colorScheme.primary, const Color(0xFF202020));
        expect(theme.colorScheme.onSurface, const Color(0xFF303030));
        expect(theme.colorScheme.onSurfaceVariant, const Color(0xFF404040));
        expect(theme.appBarTheme.foregroundColor, const Color(0xFF303030));
      }
    });
  });

  test('cachedBranding survives the v2 -> v3 schema bump shape', () async {
    final database = AppDatabase.forTesting(NativeDatabase.memory());
    addTearDown(database.close);

    await database
        .into(database.cachedBranding)
        .insertOnConflictUpdate(
          CachedBrandingCompanion.insert(
            tenantId: 'tenant-1',
            lastSyncedAt: DateTime.utc(2026, 1, 1),
            backgroundColorHex: const Value('#101010'),
            accentColorHex: const Value('#202020'),
            primaryTextColorHex: const Value('#303030'),
            secondaryTextColorHex: const Value('#404040'),
          ),
        );

    final row = await database.select(database.cachedBranding).getSingle();
    final colors = BrandingColors.fromCachedBranding(row);

    expect(colors.background, const Color(0xFF101010));
    expect(colors.accent, const Color(0xFF202020));
    expect(colors.primaryText, const Color(0xFF303030));
    expect(colors.secondaryText, const Color(0xFF404040));
  });
}
