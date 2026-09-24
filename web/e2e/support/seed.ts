import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export interface DeviceLogin {
  code: string;
  pin: string;
}

/** What global setup created. Written to disk so every test file can read it. */
export interface Seed {
  branchId: string;
  owner: { email: string; password: string; pin: string };
  register: { code: string };
  pins: { admin: string; manager: string; cashier: string; warehouse: string };
  kiosk: DeviceLogin;
  orderBoard: DeviceLogin;
  kitchen: DeviceLogin;
  items: Record<string, { barcode: string; price: number }>;
}

export function readSeed(): Seed {
  return JSON.parse(readFileSync(path.join(path.dirname(fileURLToPath(import.meta.url)), '..', '.seed', 'seed.json'), 'utf8')) as Seed;
}
