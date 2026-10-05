import { beforeEach, describe, expect, it } from 'vitest';
import { DEFAULT_CONFIG, kioskSlipReady, sanitizeConfig, useHardwareConfig } from './config';

beforeEach(() => window.localStorage.clear());

describe('sanitizeConfig', () => {
  it('keeps valid values', () => {
    const valid = { scaleProtocol: 'mettlerToledo', scaleBaudRate: 19200, paperWidth: 'mm58', autoPrintReceipt: true, kioskPrintSlip: true, kioskSlipConfirmedWidth: 'mm58' };
    expect(sanitizeConfig(valid)).toEqual(valid);
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

describe('the kiosk order slip', () => {
  beforeEach(() => useHardwareConfig.setState({ ...DEFAULT_CONFIG }));

  it('is off until it is switched on AND a test slip was confirmed on the paper in use', () => {
    expect(kioskSlipReady(DEFAULT_CONFIG)).toBe(false);
    expect(kioskSlipReady({ ...DEFAULT_CONFIG, kioskPrintSlip: true })).toBe(false);
    expect(kioskSlipReady({ ...DEFAULT_CONFIG, kioskSlipConfirmedWidth: 'mm80' })).toBe(false);
    expect(kioskSlipReady({ ...DEFAULT_CONFIG, kioskPrintSlip: true, kioskSlipConfirmedWidth: 'mm80' })).toBe(true);
  });

  it('is not ready when the confirmation was for a different paper', () => {
    expect(kioskSlipReady({ ...DEFAULT_CONFIG, paperWidth: 'mm58', kioskPrintSlip: true, kioskSlipConfirmedWidth: 'mm80' })).toBe(false);
  });

  it('switches itself off and needs a new test when the paper is changed', () => {
    useHardwareConfig.getState().update({ kioskPrintSlip: true, kioskSlipConfirmedWidth: 'mm80' });
    expect(kioskSlipReady(useHardwareConfig.getState())).toBe(true);

    useHardwareConfig.getState().update({ paperWidth: 'mm58' });
    expect(useHardwareConfig.getState().kioskPrintSlip).toBe(false);
    expect(kioskSlipReady(useHardwareConfig.getState())).toBe(false);
  });
});
