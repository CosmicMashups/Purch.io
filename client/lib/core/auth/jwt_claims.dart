import 'dart:convert';

/// Minimal JWT payload reader — reads claims back out of a stored access
/// token without verifying its signature. Not a verifier: the signature was
/// already checked server-side on every request; this only ever reads a
/// token this app itself received and stored.
Map<String, dynamic>? _decodeJwtPayload(String token) {
  final parts = token.split('.');
  if (parts.length != 3) {
    return null;
  }

  try {
    final normalized = base64Url.normalize(parts[1]);
    final payload = jsonDecode(utf8.decode(base64Url.decode(normalized)));
    return payload is Map<String, dynamic> ? payload : null;
  } catch (_) {
    return null;
  }
}

/// The "role" claim — read back out at startup so the app can decide which
/// shell to land on (staff app vs. kiosk) without a second stored flag that
/// could drift out of sync with what the token actually says.
String? roleClaimFromJwt(String token) {
  return _decodeJwtPayload(token)?['role'] as String?;
}

/// This device's own id, tenant id, and branch id, exactly as the server put
/// them in the token at login — mirrors backend JwtClaimTypes. The server is
/// still the source of truth for pairing; this is just how the client learns
/// what it was told, since nothing else on-device persists it.
class DeviceClaims {
  const DeviceClaims({
    required this.deviceId,
    required this.tenantId,
    required this.branchId,
  });

  final String deviceId;
  final String tenantId;
  final String branchId;
}

DeviceClaims? deviceClaimsFromJwt(String token) {
  final payload = _decodeJwtPayload(token);
  if (payload == null) {
    return null;
  }

  final deviceId = payload['device_id'] as String?;
  final tenantId = payload['tenant_id'] as String?;
  final branchId = payload['branch_id'] as String?;
  if (deviceId == null || tenantId == null || branchId == null) {
    return null;
  }

  return DeviceClaims(deviceId: deviceId, tenantId: tenantId, branchId: branchId);
}
