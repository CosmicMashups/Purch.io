import { pinProblem } from './staffRules';
import { DeviceType } from './types';

/** Kiosks, order boards, kitchen displays and warehouse devices sign in with a pairing PIN; a register does not need one. */
export function pinRequiredFor(deviceType: number): boolean {
  return deviceType !== DeviceType.Register;
}

/** The message to show for a device PIN, or null when it is fine. A register may leave it blank. */
export function devicePinProblem(deviceType: number, pin: string): string | null {
  if (pin.trim() === '') return pinRequiredFor(deviceType) ? 'A pairing PIN is required for this device type' : null;
  return pinProblem(pin);
}
