import { useEffect, useMemo, type ReactNode } from 'react';
import { useSession } from '../features/auth/useSession';
import { useTenantSettings } from '../features/tenant/queries';
import type { TenantSettings } from '../features/tenant/types';
import { brandingToCssVars, type TenantBranding } from './branding';
import { writeBrandCache, readBrandCache } from './brandCache';

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
    if (branding) writeBrandCache(branding);
  }, [branding]);

  useEffect(() => {
    if (!signedIn) {
      // The previous tenant's look must not linger on the sign-in screen for the next one.
      writeBrandCache(null);
      applyVars({});
      return;
    }
    applyVars(brandingToCssVars(branding ?? readBrandCache()));
  }, [signedIn, branding]);

  return <>{children}</>;
}
