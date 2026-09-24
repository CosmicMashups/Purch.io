import { describe, expect, it } from 'vitest';
import { parseCas, parseMettlerToledo, parseScaleLine, READING_MAX_AGE_MS, scaleState, toKilograms, usableKilograms } from './parser';

const NOW = 1_000_000;

describe('parseCas', () => {
  it('reads a stable line', () => {
    expect(parseCas('ST,GS,  1.250kg', NOW)).toMatchObject({ weight: 1.25, unit: 'kg', isStable: true, isOverload: false, isTare: false });
  });

  it('reads a moving line as not stable', () => {
    expect(parseCas('US,GS,  0.730kg', NOW)).toMatchObject({ weight: 0.73, isStable: false });
  });

  it('marks a tared line', () => {
    expect(parseCas('ST,NT,  0.500 kg', NOW)?.isTare).toBe(true);
  });

  it('reads negative weights and grams', () => {
    expect(parseCas('ST,GS, -0.020 g', NOW)).toMatchObject({ weight: -0.02, unit: 'g' });
  });

  it('reports overload with no weight', () => {
    expect(parseCas('OL,GS,', NOW)).toMatchObject({ isOverload: true, weight: 0, isStable: false });
  });

  it('flags a zero platter', () => {
    expect(parseCas('ST,GS,  0.000kg', NOW)?.isZero).toBe(true);
  });

  it('ignores control characters and blank lines', () => {
    expect(parseCas('\x02ST,GS,  2.000kg\x03', NOW)?.weight).toBe(2);
    expect(parseCas('   ', NOW)).toBeNull();
  });

  it('falls back to a bare number, never trusting it as settled', () => {
    expect(parseCas('  1.5 kg', NOW)).toMatchObject({ weight: 1.5, isStable: false });
  });

  it('gives up on text with no number', () => {
    expect(parseCas('ERROR', NOW)).toBeNull();
  });
});

describe('parseMettlerToledo', () => {
  it('reads MT-SICS stable and dynamic replies', () => {
    expect(parseMettlerToledo('S S     1.250 kg', NOW)).toMatchObject({ weight: 1.25, isStable: true });
    expect(parseMettlerToledo('S D     1.250 kg', NOW)).toMatchObject({ isStable: false });
  });

  it('reports overload', () => {
    expect(parseMettlerToledo('S +', NOW)?.isOverload).toBe(true);
    expect(parseMettlerToledo('OL', NOW)?.isOverload).toBe(true);
  });

  it('accepts continuous output as stable', () => {
    expect(parseMettlerToledo('  0.840 kg', NOW)).toMatchObject({ weight: 0.84, isStable: true });
  });
});

describe('parseScaleLine', () => {
  it('uses the chosen protocol', () => {
    expect(parseScaleLine('cas', 'ST,GS,  1.000kg', NOW)?.isStable).toBe(true);
    expect(parseScaleLine('mettlerToledo', 'S S 1.000 kg', NOW)?.isStable).toBe(true);
  });
});

describe('toKilograms', () => {
  it('converts the units it knows', () => {
    expect(toKilograms(1.5, 'kg')).toBe(1.5);
    expect(toKilograms(250, 'g')).toBe(0.25);
    expect(toKilograms(2, 'lb')).toBe(0.907);
  });

  it('refuses a unit it does not know', () => {
    expect(toKilograms(1, 'oz')).toBeNull();
  });
});

describe('usableKilograms', () => {
  const stable = parseCas('ST,GS,  1.250kg', NOW)!;

  it('accepts a fresh stable reading', () => {
    expect(usableKilograms(stable, NOW + 100)).toBe(1.25);
  });

  it('rejects a reading that is still settling', () => {
    expect(usableKilograms(parseCas('US,GS,  1.250kg', NOW), NOW)).toBeNull();
  });

  it('rejects overload, an empty platter and an unknown unit', () => {
    expect(usableKilograms(parseCas('OL,GS,', NOW), NOW)).toBeNull();
    expect(usableKilograms(parseCas('ST,GS,  0.000kg', NOW), NOW)).toBeNull();
    expect(usableKilograms({ ...stable, unit: 'oz' }, NOW)).toBeNull();
  });

  it('rejects a reading from a scale that has gone quiet', () => {
    expect(usableKilograms(stable, NOW + READING_MAX_AGE_MS + 1)).toBeNull();
  });

  it('rejects nothing at all', () => {
    expect(usableKilograms(null, NOW)).toBeNull();
  });
});

describe('scaleState', () => {
  it('names what the scale is doing', () => {
    expect(scaleState(null, NOW)).toBe('quiet');
    expect(scaleState(parseCas('ST,GS,  1.000kg', NOW), NOW)).toBe('stable');
    expect(scaleState(parseCas('US,GS,  1.000kg', NOW), NOW)).toBe('settling');
    expect(scaleState(parseCas('OL,GS,', NOW), NOW)).toBe('overload');
    expect(scaleState(parseCas('ST,GS,  1.000kg', NOW), NOW + READING_MAX_AGE_MS + 1)).toBe('quiet');
  });
});
