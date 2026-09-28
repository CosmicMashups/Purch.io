import { describe, expect, it } from 'vitest';
import scenarioFile from '../../../../../shared/pricing-scenarios.json';
import { priceCart, type LineDiscount, type PricingLineInput, type PricingResult, type PricingRules } from './pricingEngine';

/** The same cases the Dart engine and the backend calculator run, so the copies cannot drift apart. */
interface Scenario {
  name: string;
  now?: string;
  lines: PricingLineInput[];
  rules?: PricingRules;
  seniorPwdApplied?: boolean;
  promoCode?: string;
  expect: Partial<Omit<PricingResult, 'lineDiscounts'>> & { lineDiscounts?: Record<string, Partial<LineDiscount>> };
}

const file = scenarioFile as unknown as { now: string; scenarios: Scenario[] };

const TOLERANCE = 0.02;

describe('pricingEngine: shared scenarios', () => {
  it.each(file.scenarios.map((s) => [s.name, s] as const))('%s', (_name, scenario) => {
    const result = priceCart({
      lines: scenario.lines,
      rules: scenario.rules,
      seniorPwdApplied: scenario.seniorPwdApplied,
      promoCode: scenario.promoCode,
      now: new Date(scenario.now ?? file.now),
    });

    const { lineDiscounts, ...scalars } = scenario.expect;
    for (const [key, expected] of Object.entries(scalars)) {
      const actual = result[key as keyof PricingResult];
      if (typeof expected === 'number') expect(actual as number).toBeCloseTo(expected, 1);
      else expect(actual).toEqual(expected);
    }
    for (const [lineId, expected] of Object.entries(lineDiscounts ?? {})) {
      if (expected.discount !== undefined) expect(Math.abs(result.lineDiscounts[lineId].discount - expected.discount)).toBeLessThan(TOLERANCE);
      if (expected.label !== undefined) expect(result.lineDiscounts[lineId].label).toBe(expected.label);
    }
  });
});
