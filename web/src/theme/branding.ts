export interface TenantBranding {
  logoUrl: string | null;
  backgroundColorHex: string | null;
  accentColorHex: string | null;
  primaryTextColorHex: string | null;
  secondaryTextColorHex: string | null;
  fontFamily: string | null;
}

const HEX = /^#[0-9a-fA-F]{6}$/;
const SAFE_FONT = /^[A-Za-z0-9 _-]{1,64}$/;

export function validHex(value: string | null | undefined): string | null {
  return value && HEX.test(value) ? value.toLowerCase() : null;
}

function channel(value: number): number {
  const s = value / 255;
  return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
}

function luminance(hex: string): number {
  const n = parseInt(hex.slice(1), 16);
  return 0.2126 * channel((n >> 16) & 255) + 0.7152 * channel((n >> 8) & 255) + 0.0722 * channel(n & 255);
}

/** WCAG contrast ratio between two 6-digit hex colours. */
export function contrastRatio(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

/** White or near-black, whichever is more readable on the given brand colour. */
export function readableOn(hex: string): string {
  return contrastRatio(hex, '#ffffff') >= contrastRatio(hex, '#0f172a') ? '#ffffff' : '#0f172a';
}

function mix(hex: string, toward: string, amount: number): string {
  const a = parseInt(hex.slice(1), 16);
  const b = parseInt(toward.slice(1), 16);
  const part = (shift: number) => {
    const from = (a >> shift) & 255;
    const to = (b >> shift) & 255;
    return Math.round(from + (to - from) * amount);
  };
  return `#${((part(16) << 16) | (part(8) << 8) | part(0)).toString(16).padStart(6, '0')}`;
}

/** A pale tenant colour would vanish as a chart line on white, so it is darkened until a mark stays visible (3:1). */
export function chartAccent(accent: string, surface = '#ffffff'): string {
  let color = accent;
  for (let step = 1; step <= 10 && contrastRatio(color, surface) < 3; step += 1) color = mix(accent, '#000000', step * 0.1);
  return color;
}

/**
 * Maps tenant branding to CSS variables. Anything malformed is dropped so a bad value from the
 * server can never break the page or inject CSS. Hover colour is derived so it stays readable.
 */
export function brandingToCssVars(branding: Partial<TenantBranding> | null): Record<string, string> {
  const vars: Record<string, string> = {};
  if (!branding) return vars;

  const accent = validHex(branding.accentColorHex);
  if (accent) {
    vars['--brand'] = accent;
    vars['--brand-strong'] = mix(accent, '#000000', 0.2);
    vars['--on-brand'] = readableOn(accent);
    vars['--brand-tint'] = mix(accent, '#ffffff', 0.92);
    vars['--viz-accent'] = chartAccent(accent);
  }

  const canvas = validHex(branding.backgroundColorHex);
  const ink = validHex(branding.primaryTextColorHex);
  // A tenant palette that fails contrast would make the POS unreadable, so it is ignored.
  if (canvas && ink && contrastRatio(canvas, ink) >= 4.5) {
    vars['--canvas'] = canvas;
    vars['--ink'] = ink;
  }

  const soft = validHex(branding.secondaryTextColorHex);
  if (soft && contrastRatio(vars['--canvas'] ?? '#f8fafc', soft) >= 4.5) {
    vars['--ink-soft'] = soft;
  }

  if (branding.fontFamily && SAFE_FONT.test(branding.fontFamily)) {
    vars['--font-sans'] = `'${branding.fontFamily}', system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif`;
  }

  return vars;
}
