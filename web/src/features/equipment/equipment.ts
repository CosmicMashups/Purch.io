import { z } from 'zod';
import { EquipmentKind, EquipmentStatus } from './types';

export const KIND_LABEL: Record<EquipmentKind, string> = {
  [EquipmentKind.Equipment]: 'Equipment',
  [EquipmentKind.Furniture]: 'Furniture',
  [EquipmentKind.Utensil]: 'Utensils',
  [EquipmentKind.Other]: 'Other',
};

export const KIND_ORDER: readonly EquipmentKind[] = [EquipmentKind.Equipment, EquipmentKind.Furniture, EquipmentKind.Utensil, EquipmentKind.Other];

export const STATUS_LABEL: Record<EquipmentStatus, string> = {
  [EquipmentStatus.Operational]: 'Operational',
  [EquipmentStatus.NeedsRepair]: 'Needs repair',
  [EquipmentStatus.OutOfService]: 'Out of service',
};

export const STATUS_TONE: Record<EquipmentStatus, 'brand' | 'warn' | 'danger'> = {
  [EquipmentStatus.Operational]: 'brand',
  [EquipmentStatus.NeedsRepair]: 'warn',
  [EquipmentStatus.OutOfService]: 'danger',
};

export const equipmentSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name'),
  /** Select values are strings; converted to EquipmentKind on save. */
  kind: z.string(),
  /** Blank means a single tracked asset rather than a counted group. */
  quantity: z.string(),
  location: z.string(),
  notes: z.string(),
  isActive: z.boolean(),
});

export type EquipmentForm = z.infer<typeof equipmentSchema>;

/** A blank quantity means "not counted" (null). Anything typed must be a whole number that is not negative. */
export function parseQuantity(text: string): { ok: true; value: number | null } | { ok: false; message: string } {
  const trimmed = text.trim();
  if (trimmed === '') return { ok: true, value: null };
  const value = Number(trimmed);
  if (!Number.isInteger(value)) return { ok: false, message: 'Enter a whole number' };
  if (value < 0) return { ok: false, message: 'Cannot be negative' };
  return { ok: true, value };
}
