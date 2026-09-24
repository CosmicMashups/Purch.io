export type ScaleProtocol = 'cas' | 'mettlerToledo';

export interface ScaleReading {
  weight: number;
  unit: string;
  isStable: boolean;
  isZero: boolean;
  isOverload: boolean;
  isTare: boolean;
  /** When this reading arrived, so a scale that stopped talking is never mistaken for a settled weight. */
  receivedAt: number;
  raw: string;
}

/** Scales frame lines with control characters (STX, ETX and so on); they are blanked out before parsing. */
function stripControlChars(text: string): string {
  return Array.from(text, (c) => (c.charCodeAt(0) < 32 ? ' ' : c)).join('');
}

function reading(over: Partial<ScaleReading> & { weight: number; raw: string; now: number }): ScaleReading {
  const { now, ...rest } = over;
  return {
    unit: 'kg',
    isStable: false,
    isZero: Math.abs(over.weight) < 0.001,
    isOverload: false,
    isTare: false,
    receivedAt: now,
    ...rest,
  };
}

function overload(raw: string, now: number): ScaleReading {
  return reading({ weight: 0, isZero: false, isOverload: true, raw, now });
}

function toNumber(text: string | undefined): number {
  const n = Number((text ?? '0').replace(/\s/g, ''));
  return Number.isFinite(n) ? n : 0;
}

function regexFallback(cleaned: string, raw: string, defaultStable: boolean, now: number): ScaleReading | null {
  const match = /([+-]?\d+\.?\d*)\s*(kg|g|lb)?/i.exec(cleaned);
  if (!match) return null;
  return reading({ weight: toNumber(match[1]), unit: match[2]?.toLowerCase() ?? 'kg', isStable: defaultStable, raw, now });
}

/** CAS AP-1, ER Plus, SW-1 and PD-II lines, such as "ST,GS,  1.250kg". Ported from the Flutter client. */
export function parseCas(rawLine: string, now = Date.now()): ScaleReading | null {
  const cleaned = stripControlChars(rawLine).trim();
  if (!cleaned) return null;

  const parts = cleaned.split(',');
  if (parts.length >= 2) {
    const header = parts[0].trim().toUpperCase();
    if (header === 'OL') return overload(rawLine, now);

    const segment = parts.length >= 3 ? parts[2] : parts[1];
    const isTare = parts.length >= 3 && parts[1].trim().toUpperCase() === 'NT';
    const match = /([+-]?\s*\d+\.?\d*)\s*([a-zA-Z]+)?/.exec(segment);
    if (match) {
      return reading({ weight: toNumber(match[1]), unit: match[2]?.toLowerCase() ?? 'kg', isStable: header === 'ST', isTare, raw: rawLine, now });
    }
  }
  return regexFallback(cleaned, rawLine, false, now);
}

/** Mettler-Toledo MT-SICS ("S S 1.250 kg") and Toledo 8217 continuous output. Ported from the Flutter client. */
export function parseMettlerToledo(rawLine: string, now = Date.now()): ScaleReading | null {
  const cleaned = stripControlChars(rawLine).trim();
  if (!cleaned) return null;
  if (cleaned.includes('S +') || cleaned.includes('OL')) return overload(rawLine, now);

  const sics = /S\s+([SDI])\s+([+-]?\d+\.?\d*)\s*([a-zA-Z]+)?/i.exec(cleaned);
  if (sics) {
    return reading({ weight: toNumber(sics[2]), unit: sics[3]?.toLowerCase() ?? 'kg', isStable: sics[1].toUpperCase() === 'S', raw: rawLine, now });
  }
  return regexFallback(cleaned, rawLine, true, now);
}

export function parseScaleLine(protocol: ScaleProtocol, line: string, now = Date.now()): ScaleReading | null {
  return protocol === 'cas' ? parseCas(line, now) : parseMettlerToledo(line, now);
}

const LB_TO_KG = 0.45359237;

/** Cart quantities for weighed items are in kilograms. An unrecognised unit is never guessed. */
export function toKilograms(weight: number, unit: string): number | null {
  const u = unit.toLowerCase();
  const kg = u === 'kg' ? weight : u === 'g' ? weight / 1000 : u === 'lb' || u === 'lbs' ? weight * LB_TO_KG : null;
  return kg === null ? null : Math.round(kg * 1000) / 1000;
}

/** A reading older than this is treated as if the scale went quiet. */
export const READING_MAX_AGE_MS = 2000;

/**
 * The weight that may be put on a sale: settled, not overloaded, above zero, in a known unit, and recent.
 * Anything else returns null, so the till can never lock in a moving or stale number.
 */
export function usableKilograms(r: ScaleReading | null, now = Date.now()): number | null {
  if (!r || !r.isStable || r.isOverload) return null;
  if (now - r.receivedAt > READING_MAX_AGE_MS) return null;
  const kg = toKilograms(r.weight, r.unit);
  return kg !== null && kg > 0 ? kg : null;
}

export type ScaleState = 'settling' | 'stable' | 'overload' | 'quiet';

export function scaleState(r: ScaleReading | null, now = Date.now()): ScaleState {
  if (!r || now - r.receivedAt > READING_MAX_AGE_MS) return 'quiet';
  if (r.isOverload) return 'overload';
  return r.isStable ? 'stable' : 'settling';
}
