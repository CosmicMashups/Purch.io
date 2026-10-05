import { create } from 'zustand';
import type { ScaleProtocol } from './scale/parser';

export type PaperWidth = 'mm58' | 'mm80';

/** Settings for this browser's peripherals. They belong to the workstation, so they are never sent to the server. */
export interface HardwareConfig {
  scaleProtocol: ScaleProtocol;
  scaleBaudRate: number;
  paperWidth: PaperWidth;
  /** Open the print window by itself when a sale is completed. */
  autoPrintReceipt: boolean;
  /** Kiosk only: print the order slip when an order is sent. Only takes effect once a test slip was confirmed for this paper width. */
  kioskPrintSlip: boolean;
  /** The paper width a test slip was last confirmed to print correctly on, or null if never. Changing the paper makes it stale. */
  kioskSlipConfirmedWidth: PaperWidth | null;
}

/** Whether the kiosk should print its order slip: switched on, and the printer was proven on the paper now selected. */
export function kioskSlipReady(config: Pick<HardwareConfig, 'kioskPrintSlip' | 'kioskSlipConfirmedWidth' | 'paperWidth'>): boolean {
  return config.kioskPrintSlip && config.kioskSlipConfirmedWidth === config.paperWidth;
}

export const BAUD_RATES = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200] as const;

export const DEFAULT_CONFIG: HardwareConfig = {
  scaleProtocol: 'cas',
  scaleBaudRate: 9600,
  paperWidth: 'mm80',
  autoPrintReceipt: false,
  kioskPrintSlip: false,
  kioskSlipConfirmedWidth: null,
};

const KEY = 'purch.hardware';

/** Anything unreadable or out of range falls back to its default, field by field. */
export function sanitizeConfig(value: unknown): HardwareConfig {
  const v = typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
  return {
    scaleProtocol: v.scaleProtocol === 'cas' || v.scaleProtocol === 'mettlerToledo' ? v.scaleProtocol : DEFAULT_CONFIG.scaleProtocol,
    scaleBaudRate: typeof v.scaleBaudRate === 'number' && (BAUD_RATES as readonly number[]).includes(v.scaleBaudRate) ? v.scaleBaudRate : DEFAULT_CONFIG.scaleBaudRate,
    paperWidth: v.paperWidth === 'mm58' || v.paperWidth === 'mm80' ? v.paperWidth : DEFAULT_CONFIG.paperWidth,
    autoPrintReceipt: typeof v.autoPrintReceipt === 'boolean' ? v.autoPrintReceipt : DEFAULT_CONFIG.autoPrintReceipt,
    kioskPrintSlip: typeof v.kioskPrintSlip === 'boolean' ? v.kioskPrintSlip : DEFAULT_CONFIG.kioskPrintSlip,
    kioskSlipConfirmedWidth: v.kioskSlipConfirmedWidth === 'mm58' || v.kioskSlipConfirmedWidth === 'mm80' ? v.kioskSlipConfirmedWidth : DEFAULT_CONFIG.kioskSlipConfirmedWidth,
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
    // A printer proven on one paper is not proven on another: switching paper turns the kiosk slip off until it is tested again.
    const paperChanged = patch.paperWidth !== undefined && patch.paperWidth !== current.paperWidth;
    const next = sanitizeConfig({ ...current, ...patch, ...(paperChanged ? { kioskPrintSlip: false } : {}) });
    save(next);
    set(next);
  },
}));
