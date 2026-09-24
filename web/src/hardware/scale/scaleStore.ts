import { create } from 'zustand';
import { useHardwareConfig } from '../config';
import type { ScaleReading } from './parser';
import { SerialScale, serialApi, type ScaleStatus } from './serialScale';

interface ScaleState {
  status: ScaleStatus;
  reading: ScaleReading | null;
  message: string | null;
  scale: SerialScale | null;
  /** Asks the person to pick the scale's port. Must be called from a click. */
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  zero: () => Promise<void>;
  tare: () => Promise<void>;
}

export const useScale = create<ScaleState>((set, get) => ({
  status: 'disconnected',
  reading: null,
  message: null,
  scale: null,

  connect: async () => {
    const api = serialApi();
    if (!api) {
      set({ status: 'error', message: 'This browser cannot connect to a scale. Use Chrome or Edge.' });
      return;
    }
    await get().scale?.disconnect();
    let port;
    try {
      port = await api.requestPort();
    } catch {
      // Closing the port picker is a choice, not a failure.
      return;
    }
    const { scaleProtocol, scaleBaudRate } = useHardwareConfig.getState();
    const scale = new SerialScale({
      port,
      protocol: scaleProtocol,
      baudRate: scaleBaudRate,
      onReading: (reading) => set({ reading }),
      onStatus: (status, message) => set({ status, message: message ?? null, ...(status === 'connected' ? {} : { reading: null }) }),
    });
    set({ scale, reading: null, message: null });
    await scale.connect();
  },

  disconnect: async () => {
    await get().scale?.disconnect();
    set({ scale: null, reading: null });
  },

  zero: async () => {
    await get().scale?.zero();
  },
  tare: async () => {
    await get().scale?.tare();
  },
}));
