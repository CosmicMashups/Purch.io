import { describe, expect, it } from 'vitest';
import { median, paretoRows, quadrantOf, waterfallBars } from './chartData';

describe('waterfallBars', () => {
  it('starts a delta where the last bar ended and ends on the running total', () => {
    const bars = waterfallBars([
      { key: 'gross', label: 'Gross', value: 1000, kind: 'total' },
      { key: 'disc', label: 'Discounts', value: -150, kind: 'delta' },
      { key: 'ref', label: 'Refunds', value: -50, kind: 'delta' },
      { key: 'net', label: 'Net', value: 800, kind: 'total' },
    ]);

    expect(bars.map((b) => [b.from, b.to])).toEqual([
      [0, 1000],
      [1000, 850],
      [850, 800],
      [0, 800],
    ]);
  });
});

describe('paretoRows', () => {
  const items = [
    { key: 'a', label: 'Drinks', value: 600 },
    { key: 'b', label: 'Snacks', value: 300 },
    { key: 'c', label: 'Other goods', value: 100 },
    { key: 'z', label: 'Nothing', value: 0 },
  ];

  it('sorts biggest first, carries the running share and drops zero rows', () => {
    const rows = paretoRows(items);

    expect(rows.map((r) => r.label)).toEqual(['Drinks', 'Snacks', 'Other goods']);
    expect(rows.map((r) => r.cumulativePercent)).toEqual([60, 90, 100]);
  });

  it('folds the tail into one Other row', () => {
    const rows = paretoRows(items, 1);

    expect(rows).toHaveLength(2);
    expect(rows[1]).toMatchObject({ key: 'other', label: 'Other (2)', value: 400, cumulativePercent: 100 });
  });

  it('is empty when there is nothing to total', () => {
    expect(paretoRows([{ key: 'a', label: 'A', value: 0 }])).toEqual([]);
  });
});

describe('median and quadrants', () => {
  it('takes the middle of an odd and the mean of the middle two of an even list', () => {
    expect(median([5, 1, 3])).toBe(3);
    expect(median([1, 2, 3, 10])).toBe(2.5);
    expect(median([])).toBe(0);
  });

  it('places a point by whether it is above or below each median', () => {
    expect(quadrantOf(10, 40, 5, 30)).toBe('star');
    expect(quadrantOf(10, 20, 5, 30)).toBe('volume');
    expect(quadrantOf(2, 40, 5, 30)).toBe('niche');
    expect(quadrantOf(2, 20, 5, 30)).toBe('review');
  });
});
