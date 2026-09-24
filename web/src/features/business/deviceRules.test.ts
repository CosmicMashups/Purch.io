import { describe, expect, it } from 'vitest';
import { devicePinProblem, pinRequiredFor } from './deviceRules';
import { DeviceType } from './types';

describe('pinRequiredFor', () => {
  it('needs a PIN for every device type except a register', () => {
    expect(pinRequiredFor(DeviceType.Register)).toBe(false);
    for (const t of [DeviceType.Kiosk, DeviceType.OrderBoard, DeviceType.KitchenDisplay, DeviceType.WarehouseOfficer]) {
      expect(pinRequiredFor(t)).toBe(true);
    }
  });
});

describe('devicePinProblem', () => {
  it('lets a register skip the PIN', () => {
    expect(devicePinProblem(DeviceType.Register, '')).toBeNull();
  });

  it('insists on a PIN for a kiosk', () => {
    expect(devicePinProblem(DeviceType.Kiosk, '  ')).toBe('A pairing PIN is required for this device type');
  });

  it('applies the 4 to 8 digit rule whenever a PIN is typed', () => {
    expect(devicePinProblem(DeviceType.Register, '12')).toBe('Use 4 to 8 digits');
    expect(devicePinProblem(DeviceType.Kiosk, '123456')).toBeNull();
  });
});
