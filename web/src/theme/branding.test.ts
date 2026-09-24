import { describe, expect, it } from 'vitest';
import { brandingToCssVars, contrastRatio, readableOn, validHex } from './branding';

describe('validHex', () => {
  it('accepts 6-digit hex and normalises case', () => {
    expect(validHex('#0F766E')).toBe('#0f766e');
  });

  it('rejects anything that is not a plain hex colour', () => {
    expect(validHex('red')).toBeNull();
    expect(validHex('#fff')).toBeNull();
    expect(validHex('#0f766e; background:url(x)')).toBeNull();
    expect(validHex(null)).toBeNull();
  });
});

describe('readableOn', () => {
  it('uses white on dark brand colours and dark ink on light ones', () => {
    expect(readableOn('#0f766e')).toBe('#ffffff');
    expect(readableOn('#fde047')).toBe('#0f172a');
  });
});

describe('brandingToCssVars', () => {
  it('returns nothing without branding', () => {
    expect(brandingToCssVars(null)).toEqual({});
  });

  it('derives brand variables from the accent colour', () => {
    const vars = brandingToCssVars({ accentColorHex: '#b91c1c' });
    expect(vars['--brand']).toBe('#b91c1c');
    expect(vars['--on-brand']).toBe('#ffffff');
    expect(contrastRatio(vars['--brand-strong'], '#ffffff')).toBeGreaterThan(contrastRatio('#b91c1c', '#ffffff'));
  });

  it('ignores a background/text pair that fails contrast', () => {
    const vars = brandingToCssVars({ backgroundColorHex: '#ffffff', primaryTextColorHex: '#eeeeee' });
    expect(vars['--canvas']).toBeUndefined();
    expect(vars['--ink']).toBeUndefined();
  });

  it('accepts a readable pair', () => {
    const vars = brandingToCssVars({ backgroundColorHex: '#fffbeb', primaryTextColorHex: '#1c1917' });
    expect(vars['--canvas']).toBe('#fffbeb');
  });

  it('drops unsafe font names', () => {
    expect(brandingToCssVars({ fontFamily: 'Roboto; } body{display:none' })['--font-sans']).toBeUndefined();
    expect(brandingToCssVars({ fontFamily: 'Poppins' })['--font-sans']).toContain("'Poppins'");
  });
});
