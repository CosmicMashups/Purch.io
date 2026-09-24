import { describe, expect, it } from 'vitest';
import { brandingSchema, brandingWarnings, parseRetentionDays, toBirBody, toBrandingBody, type BrandingForm } from './settingsRules';

const blank: BrandingForm = {
  logoUrl: '',
  kioskPosterImageUrl: '',
  accentColorHex: '',
  backgroundColorHex: '',
  primaryTextColorHex: '',
  secondaryTextColorHex: '',
  fontFamily: '',
};

describe('brandingSchema', () => {
  it('accepts blank fields and valid hex colours', () => {
    expect(brandingSchema.safeParse(blank).success).toBe(true);
    expect(brandingSchema.safeParse({ ...blank, accentColorHex: '#0F766E', fontFamily: 'Poppins' }).success).toBe(true);
  });

  it('rejects colours the API would refuse', () => {
    expect(brandingSchema.safeParse({ ...blank, accentColorHex: 'teal' }).success).toBe(false);
    expect(brandingSchema.safeParse({ ...blank, accentColorHex: '#fff' }).success).toBe(false);
  });

  it('rejects a font name that could carry CSS', () => {
    expect(brandingSchema.safeParse({ ...blank, fontFamily: 'Roboto; } body{' }).success).toBe(false);
  });
});

describe('toBrandingBody', () => {
  it('turns blanks into null and trims', () => {
    expect(toBrandingBody({ ...blank, accentColorHex: ' #0F766E ', fontFamily: '  ' })).toMatchObject({
      accentColorHex: '#0F766E',
      fontFamily: null,
      logoUrl: null,
      kioskPosterImageUrl: null,
    });
  });
});

describe('brandingWarnings', () => {
  it('warns when text on the background is unreadable', () => {
    expect(brandingWarnings({ backgroundColorHex: '#ffffff', primaryTextColorHex: '#eeeeee', secondaryTextColorHex: '' })).toHaveLength(1);
  });

  it('is quiet for a readable palette or when colours are unset', () => {
    expect(brandingWarnings({ backgroundColorHex: '#fffbeb', primaryTextColorHex: '#1c1917', secondaryTextColorHex: '#57534e' })).toEqual([]);
    expect(brandingWarnings({ backgroundColorHex: '', primaryTextColorHex: '', secondaryTextColorHex: '' })).toEqual([]);
  });

  it('warns about a secondary text colour that fails on the default background', () => {
    expect(brandingWarnings({ backgroundColorHex: '', primaryTextColorHex: '', secondaryTextColorHex: '#cccccc' })).toHaveLength(1);
  });
});

describe('parseRetentionDays', () => {
  it('treats blank as keep forever and requires a whole number of at least one day', () => {
    expect(parseRetentionDays('')).toEqual({ ok: true, value: null });
    expect(parseRetentionDays('365')).toEqual({ ok: true, value: 365 });
    expect(parseRetentionDays('0')).toEqual({ ok: false, message: 'Must be at least 1 day' });
    expect(parseRetentionDays('1.5')).toEqual({ ok: false, message: 'Enter a whole number of days' });
    expect(parseRetentionDays('abc')).toEqual({ ok: false, message: 'Enter a whole number of days' });
  });
});

describe('toBirBody', () => {
  it('builds the request with blanks as null', () => {
    expect(toBirBody({ tin: ' 123-456-789-000 ', registeredBusinessName: '', registeredAddress: '', creditLedgerRetentionDays: '30' })).toEqual({
      ok: true,
      body: { tin: '123-456-789-000', registeredBusinessName: null, registeredAddress: null, creditLedgerRetentionDays: 30 },
    });
  });

  it('reports a bad retention value instead of building a body', () => {
    expect(toBirBody({ tin: '', registeredBusinessName: '', registeredAddress: '', creditLedgerRetentionDays: '0' })).toEqual({ ok: false, message: 'Must be at least 1 day' });
  });
});
