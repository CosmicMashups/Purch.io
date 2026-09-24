/** Mirrors Purch.Domain.Enums.PromoDiscountType (serialized as an integer). */
export const PromoDiscountType = {
  Percentage: 0,
  FixedAmount: 1,
  FixedPrice: 2,
} as const;
export type PromoDiscountType = (typeof PromoDiscountType)[keyof typeof PromoDiscountType];

export interface PromoWindow {
  startsAt: string | null;
  endsAt: string | null;
}

export interface BogoRule extends PromoWindow {
  id: string;
  name: string;
  triggerItemId: string;
  triggerQuantity: number;
  freeItemId: string;
  freeQuantity: number;
  isActive: boolean;
}

export type BogoRequest = Omit<BogoRule, 'id' | 'isActive'>;
export type UpdateBogoRequest = Omit<BogoRule, 'id'>;

export interface ComboRule extends PromoWindow {
  id: string;
  name: string;
  itemAId: string;
  itemBId: string;
  comboPrice: number;
  isActive: boolean;
}

export type ComboRequest = Omit<ComboRule, 'id' | 'isActive'>;
export type UpdateComboRequest = Omit<ComboRule, 'id'>;

export interface ItemDiscountRule extends PromoWindow {
  id: string;
  name: string;
  itemId: string;
  discountType: PromoDiscountType;
  discountValue: number;
  isActive: boolean;
}

export type ItemDiscountRequest = Omit<ItemDiscountRule, 'id' | 'isActive'>;
export type UpdateItemDiscountRequest = Omit<ItemDiscountRule, 'id'>;

export interface PromoCode {
  id: string;
  code: string;
  discountType: PromoDiscountType;
  discountValue: number;
  isActive: boolean;
  expiresAt: string | null;
}

export interface CreatePromoCodeRequest {
  code: string;
  discountType: PromoDiscountType;
  discountValue: number;
  expiresAt: string | null;
}
