export const PricingType = {
  Unit: 0,
  WeightVolume: 1,
  Bundle: 2,
  Service: 3,
  Combo: 4,
  VariantMatrix: 5,
} as const;
export type PricingType = (typeof PricingType)[keyof typeof PricingType];

export const TingiMode = {
  None: 0,
  Fixed: 1,
  Increment: 2,
} as const;
export type TingiMode = (typeof TingiMode)[keyof typeof TingiMode];

export interface Category {
  id: string;
  name: string;
  sortOrder: number;
  imageUrl: string | null;
}

export interface Item {
  id: string;
  name: string;
  sku: string | null;
  barcode: string | null;
  categoryId: string | null;
  basePrice: number;
  imageUrl: string | null;
  pricingType: PricingType;
  stockOnHand: number;
  isActive: boolean;
  departmentId: string | null;
  tingiMode: TingiMode;
  packagedSize: number | null;
  tingiIncrementStep: number | null;
  tingiAllowedSizes: number[];
  serviceDurationMinutes: number | null;
  lowStockThreshold: number | null;
  isOutOfStock: boolean;
}

export interface ModifierGroup {
  id: string;
  name: string;
  allowMultipleSelection: boolean;
  isRequired: boolean;
  modifiers: Modifier[];
}

export interface Modifier {
  id: string;
  name: string;
  priceDelta: number;
}

export interface ItemBatch {
  id: string;
  itemId: string;
  lotNumber: string | null;
  expiryDate: string | null;
  quantityReceived: number;
}

export interface BundlePromoRule {
  id: string;
  itemId: string;
  description: string;
  triggerQuantity: number;
  bundlePrice: number;
}

export interface ItemVariant {
  id: string;
  itemId: string;
  attributes: Record<string, string>;
  sku: string | null;
  priceOverride: number | null;
  imageUrl: string | null;
}

export interface ItemComboComponent {
  id: string;
  itemId: string;
  componentCategoryId: string;
  slotLabel: string;
  quantity: number;
  substitutionUpchargeAmount: number;
}

export interface Department {
  id: string;
  name: string;
  branchId: string;
}

// ---- Request bodies (field names must match backend exactly) ----

export interface CreateCategoryRequest {
  name: string;
  sortOrder: number;
  imageUrl: string | null;
}

export type UpdateCategoryRequest = CreateCategoryRequest;

export interface CreateItemRequest {
  name: string;
  sku: string | null;
  barcode: string | null;
  categoryId: string | null;
  basePrice: number;
  imageUrl: string | null;
  pricingType: PricingType;
  departmentId?: string | null;
}

export interface UpdateItemRequest {
  name: string;
  sku: string | null;
  barcode: string | null;
  categoryId: string | null;
  basePrice: number;
  imageUrl: string | null;
  isActive: boolean;
  departmentId?: string | null;
}

export interface CreateModifierGroupRequest {
  name: string;
  allowMultipleSelection: boolean;
  isRequired: boolean;
}

export interface CreateItemModifierRequest {
  name: string;
  priceDelta: number;
}

export interface AttachModifierGroupRequest {
  modifierGroupId: string;
}

export interface CreateItemBatchRequest {
  lotNumber: string | null;
  expiryDate: string | null;
  quantityReceived: number;
}

export interface CreateBundlePromoRuleRequest {
  description: string;
  triggerQuantity: number;
  bundlePrice: number;
}

export interface CreateItemVariantRequest {
  attributes: Record<string, string>;
  sku: string | null;
  priceOverride: number | null;
  imageUrl: string | null;
}

export interface CreateItemComboComponentRequest {
  componentCategoryId: string;
  slotLabel: string;
  quantity: number;
  substitutionUpchargeAmount: number;
}

export interface UpdateTingiConfigRequest {
  tingiMode: TingiMode;
  packagedSize: number | null;
  tingiIncrementStep: number | null;
  allowedSizes: number[];
}

export interface UpdateServiceDurationRequest {
  durationMinutes: number;
}

export interface UpdateItemDepartmentRequest {
  departmentId: string | null;
}

export interface UpdateLowStockThresholdRequest {
  threshold: number | null;
}
