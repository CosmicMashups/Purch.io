import { beforeEach, describe, expect, it } from 'vitest';
import { DEFAULT_CONFIG, sanitizeConfig, useHardwareConfig } from './config';

beforeEach(() => window.localStorage.clear());

describe('sanitizeConfig', () => {
  it('keeps valid values', () => {
    expect(sanitizeConfig({ scaleProtocol: 'mettlerToledo', scaleBaudRate: 19200, paperWidth: 'mm58' })).toEqual({ scaleProtocol: 'mettlerToledo', scaleBaudRate: 19200, paperWidth: 'mm58' });
  });

  it('replaces each bad field with its default and keeps the good ones', () => {
    expect(sanitizeConfig({ scaleProtocol: 'nope', scaleBaudRate: 1234, paperWidth: 'mm58' })).toEqual({ ...DEFAULT_CONFIG, paperWidth: 'mm58' });
  });

  it('survives junk', () => {
    expect(sanitizeConfig(null)).toEqual(DEFAULT_CONFIG);
    expect(sanitizeConfig('x')).toEqual(DEFAULT_CONFIG);
  });
});

describe('useHardwareConfig', () => {
  it('saves a change for the next visit', () => {
    useHardwareConfig.getState().update({ scaleBaudRate: 4800 });
    expect(useHardwareConfig.getState().scaleBaudRate).toBe(4800);
    expect(JSON.parse(window.localStorage.getItem('purch.hardware') ?? '{}').scaleBaudRate).toBe(4800);
  });

  it('refuses a value outside the supported list', () => {
    useHardwareConfig.getState().update({ scaleBaudRate: 7 });
    expect(useHardwareConfig.getState().scaleBaudRate).toBe(DEFAULT_CONFIG.scaleBaudRate);
  });
});
