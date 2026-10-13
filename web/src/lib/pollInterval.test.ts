import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { adaptiveListInterval, withJitter } from './pollInterval';

describe('withJitter', () => {
  it('stays within the ratio and never drops below one second', () => {
    for (let i = 0; i < 200; i++) {
      const v = withJitter(10_000, 0.15);
      expect(v).toBeGreaterThanOrEqual(8_500);
      expect(v).toBeLessThanOrEqual(11_500);
    }
    expect(withJitter(500, 0)).toBe(1_000);
  });
});

describe('adaptiveListInterval', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(Math, 'random').mockReturnValue(0.5); // no jitter
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  const options = { activeMs: 5_000, idleMs: 15_000, quietAfterMs: 5 * 60_000 };

  it('polls fast while rows exist and backs off after the list has been empty for the quiet period', () => {
    const interval = adaptiveListInterval(options);
    const query = { state: { data: [{ id: 1 }] as unknown } };

    expect(interval(query)).toBe(5_000);

    query.state.data = [];
    expect(interval(query)).toBe(5_000);
    vi.advanceTimersByTime(4 * 60_000);
    expect(interval(query)).toBe(5_000);
    vi.advanceTimersByTime(61_000);
    expect(interval(query)).toBe(15_000);
  });

  it('snaps back to the fast interval as soon as an order arrives', () => {
    const interval = adaptiveListInterval(options);
    const query = { state: { data: [] as unknown } };
    interval(query);
    vi.advanceTimersByTime(6 * 60_000);
    expect(interval(query)).toBe(15_000);

    query.state.data = [{ id: 2 }];
    expect(interval(query)).toBe(5_000);
  });

  it('keeps polling fast before the first answer arrives', () => {
    const interval = adaptiveListInterval(options);
    expect(interval({ state: { data: undefined } })).toBe(5_000);
  });
});
