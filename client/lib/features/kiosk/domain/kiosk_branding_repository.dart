/// Holds the branding data a kiosk terminal needs on boot -- specifically the
/// poster image URL for the landing screen (E1). Fetched from GET /kiosk/branding
/// which is scoped to Role.Kiosk (no Admin credentials required).
class KioskBranding {
  const KioskBranding({this.kioskPosterImageUrl});

  factory KioskBranding.fromJson(Map<String, dynamic> json) {
    return KioskBranding(
      kioskPosterImageUrl: json['kioskPosterImageUrl'] as String?,
    );
  }

  /// Hosted URL for the promotional poster shown full-bleed on the kiosk
  /// landing screen. Null = fall back to wordmark card.
  final String? kioskPosterImageUrl;
}

/// Repository for kiosk-terminal-specific branding data.
abstract class KioskBrandingRepository {
  Future<KioskBranding> getBranding();
}
