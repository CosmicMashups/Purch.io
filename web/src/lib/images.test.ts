import { describe, expect, it } from 'vitest';
import { BUNDLED, SAMPLE_IMAGE, resolveImage } from './images';

describe('bundled pictures', () => {
  it('finds every picture the app ships with', () => {
    expect(BUNDLED.logo).not.toBe('');
    expect(BUNDLED.wordmark).not.toBe('');
    expect(BUNDLED.kioskPoster).not.toBe('');
  });

  it('resolves each stored sample value to a real file', () => {
    for (const value of Object.values(SAMPLE_IMAGE)) expect(resolveImage(value)).not.toBeNull();
  });
});

describe('resolveImage', () => {
  it('gives nothing for nothing', () => {
    expect(resolveImage(null)).toBeNull();
    expect(resolveImage(undefined)).toBeNull();
    expect(resolveImage('   ')).toBeNull();
  });

  it('passes full URLs through untouched', () => {
    expect(resolveImage('https://cdn.example.com/a.png')).toBe('https://cdn.example.com/a.png');
    expect(resolveImage('data:image/png;base64,AAA')).toBe('data:image/png;base64,AAA');
  });

  it('serves hosted uploads from the API', () => {
    const url = resolveImage('/uploads/tenant/a.jpg') as string;
    expect(url.endsWith('/uploads/tenant/a.jpg')).toBe(true);
    expect(url).not.toBe('/uploads/tenant/a.jpg');
  });

  it('does not double a slash between the API address and the path', () => {
    expect(resolveImage('/uploads/a.jpg')).not.toMatch(/[^:]\/\/uploads/);
  });

  it('maps a bundled path to the shipped file, and an unknown one to nothing', () => {
    expect(resolveImage('assets/logo.jpg')).toBe(BUNDLED.logo);
    expect(resolveImage('assets/images/does_not_exist.jpg')).toBeNull();
  });

  it('trims stray spaces', () => {
    expect(resolveImage('  assets/logo.jpg ')).toBe(BUNDLED.logo);
  });
});
