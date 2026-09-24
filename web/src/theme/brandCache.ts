import type { TenantBranding } from './branding';

const CACHE_KEY = 'purch.branding';

export function readBrandCache(): Partial<TenantBranding> | null {
  try {
    const raw = window.localStorage.getItem(CACHE_KEY);
    return raw ? (JSON.parse(raw) as Partial<TenantBranding>) : null;
  } catch {
    return null;
  }
}

export function writeBrandCache(branding: TenantBranding | null): void {
  try {
    if (branding) window.localStorage.setItem(CACHE_KEY, JSON.stringify(branding));
    else window.localStorage.removeItem(CACHE_KEY);
  } catch {
    // Storage unavailable: the theme just falls back to defaults next load.
  }
}
