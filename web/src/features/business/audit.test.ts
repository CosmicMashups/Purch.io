import { describe, expect, it } from 'vitest';
import { auditDateBounds, shortId } from './audit';

describe('auditDateBounds', () => {
  it('leaves both ends open when no dates are chosen', () => {
    expect(auditDateBounds('', '')).toEqual({ ok: true });
  });

  it('includes the whole of both chosen days in Manila time', () => {
    expect(auditDateBounds('2026-09-01', '2026-09-30')).toEqual({ ok: true, from: '2026-08-31T16:00:00.000Z', to: '2026-09-30T16:00:00.000Z' });
  });

  it('allows a single open end', () => {
    expect(auditDateBounds('2026-09-01', '')).toEqual({ ok: true, from: '2026-08-31T16:00:00.000Z' });
    expect(auditDateBounds('', '2026-09-01')).toEqual({ ok: true, to: '2026-09-01T16:00:00.000Z' });
  });

  it('rejects an end before the start', () => {
    expect(auditDateBounds('2026-09-10', '2026-09-01')).toEqual({ ok: false, message: 'The end date is before the start date' });
  });
});

describe('shortId', () => {
  it('shows the first eight characters of the id', () => {
    expect(shortId('3fa85f64-5717-4562-b3fc-2c963f66afa6')).toBe('#3fa85f64');
  });
});
