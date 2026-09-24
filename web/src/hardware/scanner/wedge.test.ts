import { describe, expect, it } from 'vitest';
import { WedgeDetector, isEditableTarget } from './wedge';

function type(detector: WedgeDetector, text: string, start: number, gap: number): string | null {
  let t = start;
  let out: string | null = null;
  for (const ch of text) {
    out = detector.push(ch, t);
    t += gap;
  }
  return out ?? detector.push('Enter', t);
}

describe('WedgeDetector', () => {
  it('reads a fast run ending in Enter as a scan', () => {
    expect(type(new WedgeDetector(), '4800016001234', 0, 8)).toBe('4800016001234');
  });

  it('ignores slow human typing', () => {
    expect(type(new WedgeDetector(), '480001', 0, 200)).toBeNull();
  });

  it('ignores a short run even if it is fast', () => {
    expect(type(new WedgeDetector(), '12', 0, 5)).toBeNull();
  });

  it('drops the characters typed before a scan started', () => {
    const d = new WedgeDetector();
    d.push('x', 0);
    d.push('y', 10);
    expect(type(d, '9990001', 5_000, 8)).toBe('9990001');
  });

  it('handles two scans in a row', () => {
    const d = new WedgeDetector();
    expect(type(d, 'AAAA1111', 0, 8)).toBe('AAAA1111');
    expect(type(d, 'BBBB2222', 1_000, 8)).toBe('BBBB2222');
  });

  it('ignores keys that are not characters', () => {
    const d = new WedgeDetector();
    d.push('Shift', 0);
    d.push('Tab', 5);
    expect(d.push('Enter', 10)).toBeNull();
  });
});

describe('isEditableTarget', () => {
  it('recognises fields the user is typing in', () => {
    expect(isEditableTarget(document.createElement('input'))).toBe(true);
    expect(isEditableTarget(document.createElement('textarea'))).toBe(true);
    expect(isEditableTarget(document.createElement('select'))).toBe(true);
  });

  it('does not treat a button or the page as a field', () => {
    expect(isEditableTarget(document.createElement('button'))).toBe(false);
    expect(isEditableTarget(document.body)).toBe(false);
    expect(isEditableTarget(null)).toBe(false);
  });
});
