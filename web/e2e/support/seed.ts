import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

/** A device the setup paired. Its credential is what a real device keeps after the one-time code is used. */
export interface PairedDevice {
  id: string;
  credential: string;
}

/** What global setup created. Written to disk so every test file can read it. */
export interface Seed {
  branchId: string;
  owner: { email: string; password: string; pin: string };
  /** The till, a Register. People unlock it with their own PIN. */
  register: PairedDevice;
  /** The stock room's Warehouse device. */
  warehouse: PairedDevice;
  /** Each person's membership id, which is how a till knows who is unlocking it. */
  members: { admin: string; manager: string; cashier: string; warehouse: string };
  pins: { admin: string; manager: string; cashier: string; warehouse: string };
  kiosk: PairedDevice;
  orderBoard: PairedDevice;
  kitchen: PairedDevice;
  items: Record<string, { barcode: string; price: number }>;
}

export function readSeed(): Seed {
  return JSON.parse(readFileSync(path.join(path.dirname(fileURLToPath(import.meta.url)), '..', '.seed', 'seed.json'), 'utf8')) as Seed;
}
