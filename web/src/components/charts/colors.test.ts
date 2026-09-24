import { describe, expect, it } from 'vitest';
import { assignColors, slotColor } from './colors';

describe('slotColor', () => {
  it('follows the fixed order and wraps after eight', () => {
    expect(slotColor(0)).toBe('var(--viz-1)');
    expect(slotColor(7)).toBe('var(--viz-8)');
    expect(slotColor(8)).toBe('var(--viz-1)');
  });
});

describe('assignColors', () => {
  it('gives each key the same colour however the list is ordered', () => {
    const a = assignColors(['bakery', 'coffee', 'meals']);
    const b = assignColors(['meals', 'bakery', 'coffee']);
    expect(a.get('coffee')).toBe(b.get('coffee'));
    expect(a.get('bakery')).toBe('var(--viz-1)');
  });

  it('ignores duplicates', () => {
    expect(assignColors(['a', 'a', 'b']).size).toBe(2);
  });
});
