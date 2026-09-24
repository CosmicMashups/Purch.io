import { describe, expect, it } from 'vitest';
import { decodeClaims } from './jwt';

function makeToken(payload: object): string {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${b64({ alg: 'HS256' })}.${b64(payload)}.sig`;
}

describe('decodeClaims', () => {
  it('reads role, scope, tenant and expiry', () => {
    const claims = decodeClaims(
      makeToken({ sub: 's1', role: 'Cashier', tenant_id: 't1', scope_type: 'Branch', scope_id: 'b1', exp: 1_800_000_000 }),
    );
    expect(claims).toMatchObject({ staffId: 's1', role: 'Cashier', tenantId: 't1', scopeType: 'Branch', scopeId: 'b1' });
    expect(claims?.expiresAt?.getTime()).toBe(1_800_000_000_000);
  });

  it('returns null for missing or malformed tokens', () => {
    expect(decodeClaims(null)).toBeNull();
    expect(decodeClaims('not-a-jwt')).toBeNull();
    expect(decodeClaims('a.!!!.c')).toBeNull();
  });

  it('ignores unknown scope types', () => {
    expect(decodeClaims(makeToken({ scope_type: 'Galaxy' }))?.scopeType).toBeNull();
  });
});
