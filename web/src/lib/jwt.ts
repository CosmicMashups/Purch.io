export type ScopeType = 'Tenant' | 'Branch' | 'Department';

export interface TokenClaims {
  role: string | null;
  staffId: string | null;
  tenantId: string | null;
  branchId: string | null;
  deviceId: string | null;
  scopeType: ScopeType | null;
  scopeId: string | null;
  expiresAt: Date | null;
}

function decodePayload(token: string): Record<string, unknown> | null {
  const parts = token.split('.');
  if (parts.length !== 3) return null;
  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const json = new TextDecoder().decode(Uint8Array.from(atob(padded), (c) => c.charCodeAt(0)));
    const payload: unknown = JSON.parse(json);
    return typeof payload === 'object' && payload !== null ? (payload as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

function str(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

function scopeType(value: unknown): ScopeType | null {
  return value === 'Tenant' || value === 'Branch' || value === 'Department' ? value : null;
}

/** Reads claims for navigation and display only. This is not verification: the server checks every request. */
export function decodeClaims(token: string | null): TokenClaims | null {
  if (!token) return null;
  const payload = decodePayload(token);
  if (!payload) return null;
  const exp = payload.exp;
  return {
    role: str(payload.role),
    staffId: str(payload.sub),
    tenantId: str(payload.tenant_id),
    branchId: str(payload.branch_id),
    deviceId: str(payload.device_id),
    scopeType: scopeType(payload.scope_type),
    scopeId: str(payload.scope_id),
    expiresAt: typeof exp === 'number' ? new Date(exp * 1000) : null,
  };
}
