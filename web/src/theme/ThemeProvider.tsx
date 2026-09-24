import { useEffect, useMemo, type ReactNode } from 'react';
import { useSession } from '../features/auth/useSession';
import { useTenantSettings } from '../features/tenant/queries';
import type { TenantSettings } from '../features/tenant/types';
import { brandingToCssVars, type TenantBranding } from './branding';

const CACHE_KEY = 'purch.branding';

function readCache(): Partial<TenantBranding> | null {
  try {
    const raw = window.localStorage.getItem(CACHE_KEY);
    return raw ? (JSON.parse(raw) as Partial<TenantBranding>) : null;
  } catch {
    return null;
  }
}

function writeCache(branding: TenantBranding | null): void {
  try {
    if (branding) window.localStorage.setItem(CACHE_KEY, JSON.stringify(branding));
    else window.localStorage.removeItem(CACHE_KEY);
  } catch {
    // Storage unavailable: the theme just falls back to defaults next load.
  }
}

function applyVars(vars: Record<string, string>): void {
  const root = document.documentElement.style;
  for (const name of ['--brand', '--brand-strong', '--on-brand', '--brand-tint', '--canvas', '--ink', '--ink-soft', '--font-sans']) {
    root.removeProperty(name);
  }
  for (const [name, value] of Object.entries(vars)) root.setProperty(name, value);
}

function toBranding(s: TenantSettings): TenantBranding {
  return {
    logoUrl: s.brandingLogoUrl,
    backgroundColorHex: s.brandingBackgroundColorHex,
    accentColorHex: s.brandingAccentColorHex,
    primaryTextColorHex: s.brandingPrimaryTextColorHex,
    secondaryTextColorHex: s.brandingSecondaryTextColorHex,
    fontFamily: s.brandingFontFamily,
  };
}

/**
 * Applies tenant branding as CSS variables. GET /tenant/settings is Admin-only, so other roles
 * render with the last branding an Admin loaded on this device, or the Purch.io defaults.
 * See docs/REACT-MIGRATION.md gap G10.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const { claims } = useSession();
  const signedIn = claims !== null;
  const { data } = useTenantSettings();
  const branding = useMemo(() => (data ? toBranding(data) : null), [data]);

  useEffect(() => {
    if (branding) writeCache(branding);
  }, [branding]);

  useEffect(() => {
    if (!signedIn) {
      // The previous tenant's look must not linger on the sign-in screen for the next one.
      writeCache(null);
      applyVars({});
      return;
    }
    applyVars(brandingToCssVars(branding ?? readCache()));
  }, [signedIn, branding]);

  return <>{children}</>;
}
