import { describe, expect, it } from 'vitest';
import { MAX_IMAGE_BYTES, validateImageFile } from './imageRules';

describe('validateImageFile', () => {
  it('accepts supported images regardless of case', () => {
    expect(validateImageFile({ name: 'latte.JPG', size: 1000 })).toBeNull();
    expect(validateImageFile({ name: 'a.b.webp', size: 1000 })).toBeNull();
  });

  it('rejects empty, oversized and unsupported files', () => {
    expect(validateImageFile({ name: 'a.png', size: 0 })).toMatch(/empty/);
    expect(validateImageFile({ name: 'a.png', size: MAX_IMAGE_BYTES + 1 })).toMatch(/10 MB/);
    expect(validateImageFile({ name: 'a.svg', size: 100 })).toMatch(/JPG, PNG, WebP or GIF/);
    expect(validateImageFile({ name: 'noextension', size: 100 })).toMatch(/JPG, PNG, WebP or GIF/);
  });

  it('accepts a file exactly at the limit', () => {
    expect(validateImageFile({ name: 'a.gif', size: MAX_IMAGE_BYTES })).toBeNull();
  });
});
