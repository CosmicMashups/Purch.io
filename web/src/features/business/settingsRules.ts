import { z } from 'zod';
import { contrastRatio, validHex } from '../../theme/branding';
import type { UpdateBirBody, UpdateBrandingBody } from '../tenant/api';

const optionalHex = z.string().refine((v) => v === '' || validHex(v) !== null, 'Use a colour like #0F766E');

export const brandingSchema = z.object({
  logoUrl: z.string(),
  kioskPosterImageUrl: z.string(),
  accentColorHex: optionalHex,
  backgroundColorHex: optionalHex,
  primaryTextColorHex: optionalHex,
  secondaryTextColorHex: optionalHex,
  fontFamily: z.string().refine((v) => v === '' || /^[A-Za-z0-9 _-]{1,64}$/.test(v), 'Use letters, numbers, spaces, dashes or underscores'),
});

export type BrandingForm = z.infer<typeof brandingSchema>;

const blankToNull = (v: string): string | null => (v.trim() === '' ? null : v.trim());

export function toBrandingBody(v: BrandingForm): UpdateBrandingBody {
  return {
    logoUrl: blankToNull(v.logoUrl),
    kioskPosterImageUrl: blankToNull(v.kioskPosterImageUrl),
    accentColorHex: blankToNull(v.accentColorHex),
    backgroundColorHex: blankToNull(v.backgroundColorHex),
    primaryTextColorHex: blankToNull(v.primaryTextColorHex),
    secondaryTextColorHex: blankToNull(v.secondaryTextColorHex),
    fontFamily: blankToNull(v.fontFamily),
  };
}

/**
 * The app ignores a palette whose text is hard to read on its background (see brandingToCssVars),
 * so tell the person before they save something that would silently do nothing.
 */
export function brandingWarnings(v: Pick<BrandingForm, 'backgroundColorHex' | 'primaryTextColorHex' | 'secondaryTextColorHex'>): string[] {
  const warnings: string[] = [];
  const background = validHex(v.backgroundColorHex);
  const text = validHex(v.primaryTextColorHex);
  const soft = validHex(v.secondaryTextColorHex);
  if (background && text && contrastRatio(background, text) < 4.5) {
    warnings.push('The main text is hard to read on that background, so these two colours will not be used.');
  }
  if (soft && contrastRatio(background ?? '#f8fafc', soft) < 4.5) {
    warnings.push('The secondary text is hard to read on the background, so it will not be used.');
  }
  return warnings;
}

export const birSchema = z.object({
  tin: z.string().max(40, 'That is too long'),
  registeredBusinessName: z.string().max(200, 'That is too long'),
  registeredAddress: z.string().max(400, 'That is too long'),
  creditLedgerRetentionDays: z.string(),
});

export type BirForm = z.infer<typeof birSchema>;

export function parseRetentionDays(text: string): { ok: true; value: number | null } | { ok: false; message: string } {
  const trimmed = text.trim();
  if (trimmed === '') return { ok: true, value: null };
  const value = Number(trimmed);
  if (!Number.isInteger(value)) return { ok: false, message: 'Enter a whole number of days' };
  if (value < 1) return { ok: false, message: 'Must be at least 1 day' };
  return { ok: true, value };
}

export function toBirBody(v: BirForm): { ok: true; body: UpdateBirBody } | { ok: false; message: string } {
  const retention = parseRetentionDays(v.creditLedgerRetentionDays);
  if (!retention.ok) return retention;
  return {
    ok: true,
    body: {
      tin: blankToNull(v.tin),
      registeredBusinessName: blankToNull(v.registeredBusinessName),
      registeredAddress: blankToNull(v.registeredAddress),
      creditLedgerRetentionDays: retention.value,
    },
  };
}
