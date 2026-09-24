import { describe, expect, it } from 'vitest';
import { arcPath, calendarGrid, foldTail, heatLevel, linear, niceCeil, niceTicks, relativeAge, shareLabel, sliceAngles } from './scale';

describe('niceCeil and niceTicks', () => {
  it('rounds up to a clean step', () => {
    expect(niceCeil(0.9)).toBe(1);
    expect(niceCeil(1.4)).toBe(2);
    expect(niceCeil(2.3)).toBe(2.5);
    expect(niceCeil(3.1)).toBe(5);
    expect(niceCeil(7)).toBe(10);
    expect(niceCeil(1340)).toBe(2000);
  });

  it('falls back to 1 for nothing or nonsense', () => {
    expect(niceCeil(0)).toBe(1);
    expect(niceCeil(-4)).toBe(1);
    expect(niceCeil(Number.NaN)).toBe(1);
  });

  it('ends the axis on a clean value at or above the data', () => {
    expect(niceTicks(1340)).toEqual([0, 500, 1000, 1500]);
    expect(niceTicks(9200)).toEqual([0, 2500, 5000, 7500, 10000]);
  });

  it('still gives an axis when there is no data', () => {
    expect(niceTicks(0)).toEqual([0, 1]);
  });
});

describe('linear', () => {
  it('maps a domain onto a range, including an inverted one', () => {
    const y = linear([0, 100], [200, 0]);
    expect(y(0)).toBe(200);
    expect(y(100)).toBe(0);
    expect(y(50)).toBe(100);
  });

  it('does not divide by zero on a flat domain', () => {
    expect(linear([5, 5], [10, 20])(5)).toBe(10);
  });
});

describe('shareLabel', () => {
  it('rounds and never shows a real share as zero', () => {
    expect(shareLabel(1, 3)).toBe('33%');
    expect(shareLabel(1, 500)).toBe('<1%');
    expect(shareLabel(0, 10)).toBe('0%');
    expect(shareLabel(5, 0)).toBe('0%');
  });
});

describe('sliceAngles and arcPath', () => {
  it('splits a full turn in proportion, from twelve o clock', () => {
    const [a, b] = sliceAngles([1, 3]);
    expect(a.start).toBeCloseTo(-Math.PI / 2);
    expect(a.end - a.start).toBeCloseTo(Math.PI / 2);
    expect(b.end - b.start).toBeCloseTo((Math.PI * 3) / 2);
    expect(b.end).toBeCloseTo(-Math.PI / 2 + Math.PI * 2);
  });

  it('gives empty slices when everything is zero', () => {
    expect(sliceAngles([0, 0]).every((s) => s.start === s.end)).toBe(true);
  });

  it('draws a closed sector, and a full circle as two halves', () => {
    expect(arcPath(50, 50, 40, 25, 0, 1)).toMatch(/^M .* A .* L .* A .* Z$/);
    expect(arcPath(50, 50, 40, 25, 0, Math.PI * 2).match(/M /g)).toHaveLength(2);
    expect(arcPath(50, 50, 40, 25, 1, 1)).toBe('');
  });
});

describe('heatLevel', () => {
  it('is empty for nothing and always visible for anything', () => {
    expect(heatLevel(0, 100)).toBe(0);
    expect(heatLevel(0.01, 100)).toBe(1);
    expect(heatLevel(100, 100)).toBe(5);
    expect(heatLevel(50, 100)).toBe(3);
  });

  it('is empty when there is no maximum', () => {
    expect(heatLevel(5, 0)).toBe(0);
  });
});

describe('foldTail', () => {
  const items = [
    { name: 'A', v: 50 },
    { name: 'B', v: 30 },
    { name: 'C', v: 10 },
    { name: 'D', v: 6 },
    { name: 'E', v: 4 },
  ];
  const other = (rest: typeof items, total: number) => ({ name: `Other (${rest.length})`, v: total });

  it('keeps the biggest and folds the rest into one entry', () => {
    const folded = foldTail(items, 3, (i) => i.v, other);
    expect(folded.map((i) => i.name)).toEqual(['A', 'B', 'C', 'Other (2)']);
    expect(folded[3].v).toBe(10);
  });

  it('leaves a short list alone', () => {
    expect(foldTail(items, 5, (i) => i.v, other)).toBe(items);
  });
});

describe('calendarGrid', () => {
  it('puts each date in its weekday row and week column', () => {
    // 2026-09-23 is a Wednesday.
    const cells = calendarGrid(['2026-09-23', '2026-09-26', '2026-09-27', '2026-09-30']);
    expect(cells.map((c) => [c.row, c.col])).toEqual([
      [3, 0],
      [6, 0],
      [0, 1],
      [3, 1],
    ]);
  });

  it('is empty for no dates', () => {
    expect(calendarGrid([])).toEqual([]);
  });
});

describe('relativeAge', () => {
  const now = Date.parse('2026-09-24T12:00:00Z');
  it('reads naturally at each scale', () => {
    expect(relativeAge('2026-09-24T11:59:40Z', now)).toBe('just now');
    expect(relativeAge('2026-09-24T11:30:00Z', now)).toBe('30 min ago');
    expect(relativeAge('2026-09-24T09:00:00Z', now)).toBe('3 h ago');
    expect(relativeAge('2026-09-23T12:00:00Z', now)).toBe('1 day ago');
    expect(relativeAge('2026-09-20T12:00:00Z', now)).toBe('4 days ago');
  });
});
