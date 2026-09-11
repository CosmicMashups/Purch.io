import 'dart:convert';

/// Minimal JWT payload reader — just enough to read the "role" claim back out
/// of a stored access token at startup, so the app can decide which shell to
/// land on (staff app vs. kiosk) without a second stored flag that could
/// drift out of sync with what the token actually says. Not a verifier: the
/// signature was already checked server-side on every request; this only
/// ever reads a token this app itself received and stored.
String? roleClaimFromJwt(String token) {
  final parts = token.split('.');
  if (parts.length != 3) {
    return null;
  }

  try {
    final normalized = base64Url.normalize(parts[1]);
    final payload = jsonDecode(utf8.decode(base64Url.decode(normalized)));
    return payload is Map<String, dynamic> ? payload['role'] as String? : null;
  } catch (_) {
    return null;
  }
}
