import { describe, expect, it } from 'vitest';
import { pinProblem } from './staffRules';

describe('pinProblem', () => {
  it('asks for a PIN when it is blank', () => {
    expect(pinProblem('')).toBe('Enter a PIN');
    expect(pinProblem('   ')).toBe('Enter a PIN');
  });

  it('accepts 4 to 8 digits', () => {
    expect(pinProblem('1234')).toBeNull();
    expect(pinProblem('12345678')).toBeNull();
  });

  it('refuses anything shorter, longer or not all digits', () => {
    expect(pinProblem('123')).toBe('Use 4 to 8 digits');
    expect(pinProblem('123456789')).toBe('Use 4 to 8 digits');
    expect(pinProblem('12a4')).toBe('Use 4 to 8 digits');
  });
});
