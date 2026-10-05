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
  sortOrder: number;
}

export interface ModifierGroup {
  id: string;
  name: string;
  allowMultipleSelection: boolean;
  isRequired: boolean;
  modifiers: Modifier[];
  /** When set, every active item of this category is offered in the group, next to its own modifiers. */
  categoryId?: string | null;
  categoryItems?: ModifierCategoryItem[] | null;
}

/** An item offered through a category-linked group. `price` is what the customer pays (the group's override, else the item's own). */
export interface ModifierCategoryItem {
  itemId: string;
  name: string;
  imageUrl: string | null;
  basePrice: number;
  priceOverride: number | null;
  price: number;
  /** Hidden from this group. Listed so an admin can bring it back; ordering screens skip it. */
  isExcluded: boolean;
  isOutOfStock: boolean;
}

export interface UpdateModifierGroupRequest {
  name: string;
  allowMultipleSelection: boolean;
  isRequired: boolean;
  categoryId: string | null;
}

export interface UpdateModifierCategoryItemRequest {
  priceOverride: number | null;
  isExcluded: boolean;
}

export interface Modifier {
  id: string;
  name: string;
  priceDelta: number;
  /** The server's verdict from the modifier's ingredients. A sold-out modifier cannot be chosen. */
  isOutOfStock?: boolean;
  /** What choosing it uses up. Names only: stock levels are never sent to the till or the kiosk. */
  ingredients?: ModifierIngredient[];
}

/** Mirrors Purch.Application.Catalog.ModifierIngredientDto. A null quantity means "only check availability". */
export interface ModifierIngredient {
  inventoryItemId: string;
  inventoryItemName: string;
  quantityPerOrder: number | null;
}

export interface ReplaceModifierIngredientLine {
  inventoryItemId: string;
  quantityPerOrder: number | null;
}

export interface ReplaceModifierIngredientsRequest {
  lines: ReplaceModifierIngredientLine[];
}

export interface UpdateItemModifierRequest {
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

/** One part of a combo or deal: either a choice from a category, or (with `componentItemId`) one fixed item. */
export interface ItemComboComponent {
  id: string;
  componentCategoryId: string;
  componentCategoryName?: string;
  slotLabel: string;
  quantity: number;
  /** A flat charge added once for the slot, whichever item is picked. The server may send null for none. */
  substitutionUpchargeAmount: number | null;
  /** Set for a fixed slot: every unit of it is exactly this item and the customer picks nothing. */
  componentItemId?: string | null;
  componentItemName?: string | null;
  /** Extra price for particular choices in a choose slot. An item that is not listed is included. */
  choiceUpcharges?: ComboChoiceUpcharge[];
}

export interface ComboChoiceUpcharge {
  itemId: string;
  amount: number;
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
  sortOrder?: number;
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
  sortOrder?: number;
}

export interface CreateModifierGroupRequest {
  name: string;
  allowMultipleSelection: boolean;
  isRequired: boolean;
  categoryId?: string | null;
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
  /** Not used for a fixed slot: the server takes the fixed item's own category. */
  componentCategoryId: string;
  slotLabel: string;
  quantity: number;
  substitutionUpchargeAmount: number | null;
  componentItemId?: string | null;
  choiceUpcharges?: ComboChoiceUpcharge[];
}

export type UpdateItemComboComponentRequest = CreateItemComboComponentRequest;

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

/** Mirrors Purch.Application.Inventory.ItemRecipeLineDto. A null quantity means "only check availability". */
export interface ItemRecipeLine {
  inventoryItemId: string;
  inventoryItemName: string;
  quantityPerOrder: number | null;
}

export interface ReplaceItemRecipeLine {
  inventoryItemId: string;
  quantityPerOrder: number | null;
}

export interface ReplaceItemRecipeRequest {
  lines: ReplaceItemRecipeLine[];
}
