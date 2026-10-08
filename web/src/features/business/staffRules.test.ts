import { describe, expect, it } from 'vitest';
import { pinProblem } from './staffRules';

describe('pinProblem', () => {
  it('asks for a PIN when it is blank', () => {
    expect(pinProblem('')).toBe('Enter a PIN');
    expect(pinProblem('   ')).toBe('Enter a PIN');
  });

  it('accepts 6 to 8 digits', () => {
    expect(pinProblem('123456')).toBeNull();
    expect(pinProblem('12345678')).toBeNull();
  });

  it('refuses anything shorter, longer or not all digits', () => {
    expect(pinProblem('12345')).toBe('Use 6 to 8 digits');
    expect(pinProblem('123456789')).toBe('Use 6 to 8 digits');
    expect(pinProblem('12a456')).toBe('Use 6 to 8 digits');
  });
});
