import { useMemo } from 'react';
import { useAuthStore } from '../../lib/authStore';
import { decodeClaims, type TokenClaims } from '../../lib/jwt';
import { staffRoleFromClaim, type StaffRole } from '../../permissions/navPolicy';

export interface Session {
  claims: TokenClaims | null;
  /** Null for a device role (kiosk, display) or an unreadable token. */
  role: StaffRole | null;
}

/** Derived from the stored access token. For navigation only; the API enforces every action. */
export function useSession(): Session {
  const accessToken = useAuthStore((s) => s.accessToken);
  return useMemo(() => {
    const claims = decodeClaims(accessToken);
    return { claims, role: staffRoleFromClaim(claims?.role ?? null) };
  }, [accessToken]);
}
