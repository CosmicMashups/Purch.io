import { create } from 'zustand';
import type { ScaleProtocol } from './scale/parser';

export type PaperWidth = 'mm58' | 'mm80';

/** Settings for this browser's peripherals. They belong to the workstation, so they are never sent to the server. */
export interface HardwareConfig {
  scaleProtocol: ScaleProtocol;
  scaleBaudRate: number;
  paperWidth: PaperWidth;
}

export const BAUD_RATES = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200] as const;

export const DEFAULT_CONFIG: HardwareConfig = { scaleProtocol: 'cas', scaleBaudRate: 9600, paperWidth: 'mm80' };

const KEY = 'purch.hardware';

/** Anything unreadable or out of range falls back to its default, field by field. */
export function sanitizeConfig(value: unknown): HardwareConfig {
  const v = typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
  return {
    scaleProtocol: v.scaleProtocol === 'cas' || v.scaleProtocol === 'mettlerToledo' ? v.scaleProtocol : DEFAULT_CONFIG.scaleProtocol,
    scaleBaudRate: typeof v.scaleBaudRate === 'number' && (BAUD_RATES as readonly number[]).includes(v.scaleBaudRate) ? v.scaleBaudRate : DEFAULT_CONFIG.scaleBaudRate,
    paperWidth: v.paperWidth === 'mm58' || v.paperWidth === 'mm80' ? v.paperWidth : DEFAULT_CONFIG.paperWidth,
  };
}

function load(): HardwareConfig {
  try {
    const text = window.localStorage.getItem(KEY);
    return sanitizeConfig(text ? JSON.parse(text) : null);
  } catch {
    return DEFAULT_CONFIG;
  }
}

function save(config: HardwareConfig): void {
  try {
    window.localStorage.setItem(KEY, JSON.stringify(config));
  } catch {
    // Not remembering the settings only means they are chosen again next time.
  }
}

interface ConfigState extends HardwareConfig {
  update: (patch: Partial<HardwareConfig>) => void;
}

export const useHardwareConfig = create<ConfigState>((set, get) => ({
  ...load(),
  update: (patch) => {
    const { update: _update, ...current } = get();
    const next = sanitizeConfig({ ...current, ...patch });
    save(next);
    set(next);
  },
}));
