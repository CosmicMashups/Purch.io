import type { Seed } from './seed';

const API_PORT = process.env.E2E_API_PORT ?? '5099';
export const API_URL = `http://127.0.0.1:${API_PORT}`;

let counter = Math.floor(Math.random() * 60_000);

/**
 * A fresh client address. The backend rate limits sign-in per address (10 per 15 minutes) and, in the
 * mode the tests run it in, trusts X-Forwarded-For, so each test can have a bucket of its own.
 */
export function freshIp(): string {
  counter += 1;
  return `10.${(counter >> 16) & 255}.${(counter >> 8) & 255}.${(counter & 255) || 1}`;
}

export class ApiFailure extends Error {
  readonly status: number;
  constructor(method: string, path: string, status: number, body: string) {
    super(`${method} ${path} failed with ${status}: ${body.slice(0, 300)}`);
    this.status = status;
  }
}

interface CallOptions {
  token?: string;
  body?: unknown;
  ip?: string;
}

/** A direct call to the backend, used to seed data and to sign in without going through the screens. */
export async function call<T>(method: string, path: string, { token, body, ip }: CallOptions = {}): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    method,
    headers: {
      'content-type': 'application/json',
      'x-forwarded-for': ip ?? freshIp(),
      ...(token ? { authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  if (!response.ok) throw new ApiFailure(method, path, response.status, text);
  return (text ? JSON.parse(text) : undefined) as T;
}

export interface Tokens {
  accessToken: string;
  refreshToken: string;
}

/** The owner or anyone with an account, signing in with email and password on a personal device. */
export const signIn = (email: string, password: string, ip?: string) => call<Tokens>('POST', '/auth/sign-in', { body: { email, password }, ip });

/** A person unlocking a paired till or warehouse device with their own PIN. */
export const unlock = (deviceCredential: string, membershipId: string, pin: string, ip?: string) =>
  call<Tokens>('POST', '/devices/unlock', { body: { deviceCredential, membershipId, pin }, ip });

/** An unattended device (kiosk, kitchen display, order board) starting its session from its own credential. */
export const deviceSession = (deviceCredential: string, ip?: string) =>
  call<Tokens & { accessToken: string }>('POST', '/devices/session', { body: { deviceCredential }, ip });

/** Someone on the shop's till, by who they are. */
export const unlockOnTill = (seed: Seed, person: keyof Seed['members'], ip?: string) =>
  unlock(person === 'warehouse' ? seed.warehouse.credential : seed.register.credential, seed.members[person], seed.pins[person], ip);

/**
 * Leaves the register with no open lines. A cart is created on first read, so this reads it (as a manager)
 * and clears it only if something is in it. Clearing a cart needs another Admin or Manager's PIN, so the admin gives it.
 * Pass `removeCart` to remove the open cart itself, which a kiosk order needs before a till can take it.
 */
export async function resetRegister(seed: Seed, ip: string, { removeCart = false } = {}): Promise<void> {
  const boss = await unlockOnTill(seed, 'manager', ip);
  const cart = await call<{ lines: unknown[] }>('GET', '/transactions/cart', { token: boss.accessToken, ip });
  if (cart.lines.length > 0 || removeCart) await call('POST', '/transactions/cart/void', { token: boss.accessToken, ip, body: { approverPin: seed.pins.admin } });
}
