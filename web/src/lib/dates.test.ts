import { describe, expect, it } from 'vitest';
import { describeWindow, isoToLocalInput, localInputToIso } from './dates';

describe('localInputToIso / isoToLocalInput', () => {
  it('round-trips a local datetime', () => {
    const iso = localInputToIso('2026-09-24T10:30');
    expect(iso).not.toBeNull();
    expect(isoToLocalInput(iso)).toBe('2026-09-24T10:30');
  });

  it('treats blank and invalid input as absent', () => {
    expect(localInputToIso('')).toBeNull();
    expect(localInputToIso(undefined)).toBeNull();
    expect(localInputToIso('nope')).toBeNull();
    expect(isoToLocalInput(null)).toBe('');
    expect(isoToLocalInput('nope')).toBe('');
  });
});

describe('describeWindow', () => {
  it('says always on with no dates', () => {
    expect(describeWindow(null, null)).toBe('Always on');
  });

  it('describes open-ended windows', () => {
    expect(describeWindow('2026-09-24T02:00:00Z', null)).toMatch(/^From /);
    expect(describeWindow(null, '2026-09-24T02:00:00Z')).toMatch(/^Until /);
  });

  it('describes a closed window', () => {
    expect(describeWindow('2026-09-24T02:00:00Z', '2026-09-30T02:00:00Z')).toContain(' to ');
  });
});
