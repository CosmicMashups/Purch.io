/** Mirrors Purch.Domain.Enums.EquipmentStatus (serialized as an integer). */
export const EquipmentStatus = {
  Operational: 0,
  NeedsRepair: 1,
  OutOfService: 2,
} as const;
export type EquipmentStatus = (typeof EquipmentStatus)[keyof typeof EquipmentStatus];

/** Mirrors Purch.Domain.Enums.EquipmentKind (serialized as an integer). */
export const EquipmentKind = {
  Equipment: 0,
  Furniture: 1,
  Utensil: 2,
  Other: 3,
} as const;
export type EquipmentKind = (typeof EquipmentKind)[keyof typeof EquipmentKind];

/** Mirrors Purch.Application.EquipmentInventory.EquipmentDto. */
export interface Equipment {
  id: string;
  name: string;
  kind: EquipmentKind;
  status: EquipmentStatus;
  quantity: number | null;
  location: string | null;
  notes: string | null;
  isActive: boolean;
  sortOrder: number;
  /** How many items need this equipment. */
  usedByItemCount: number;
}

export interface CreateEquipmentRequest {
  name: string;
  kind: EquipmentKind;
  quantity: number | null;
  location: string | null;
  notes: string | null;
}

export interface UpdateEquipmentRequest extends CreateEquipmentRequest {
  isActive: boolean;
}

/** Mirrors Purch.Application.EquipmentInventory.ItemEquipmentDto. */
export interface ItemEquipment {
  equipmentId: string;
  equipmentName: string;
  status: EquipmentStatus;
}
