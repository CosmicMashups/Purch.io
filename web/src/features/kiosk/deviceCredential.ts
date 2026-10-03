const KEY = 'purch.deviceCredential';

/** The secret a paired device holds. It outlives sign-outs on purpose: it is what lets the device start a new session by itself. */
export function readDeviceCredential(): string | null {
  try {
    return window.localStorage.getItem(KEY);
  } catch {
    return null;
  }
}

export function saveDeviceCredential(credential: string): void {
  try {
    window.localStorage.setItem(KEY, credential);
  } catch {
    // Without storage the device simply asks for a new code next time.
  }
}

export function clearDeviceCredential(): void {
  try {
    window.localStorage.removeItem(KEY);
  } catch {
    // Nothing to clear.
  }
}
