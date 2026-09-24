import { apiClient } from '../../lib/apiClient';
import type {
  BogoRequest,
  BogoRule,
  ComboRequest,
  ComboRule,
  CreatePromoCodeRequest,
  ItemDiscountRequest,
  ItemDiscountRule,
  PromoCode,
  UpdateBogoRequest,
  UpdateComboRequest,
  UpdateItemDiscountRequest,
} from './types';

export const promotionsApi = {
  listBogo: () => apiClient.get<BogoRule[]>('/promos/bogo').then((r) => r.data),
  createBogo: (body: BogoRequest) => apiClient.post<BogoRule>('/promos/bogo', body).then((r) => r.data),
  updateBogo: (id: string, body: UpdateBogoRequest) => apiClient.put<BogoRule>(`/promos/bogo/${id}`, body).then((r) => r.data),

  listCombos: () => apiClient.get<ComboRule[]>('/promos/combos').then((r) => r.data),
  createCombo: (body: ComboRequest) => apiClient.post<ComboRule>('/promos/combos', body).then((r) => r.data),
  updateCombo: (id: string, body: UpdateComboRequest) => apiClient.put<ComboRule>(`/promos/combos/${id}`, body).then((r) => r.data),

  listItemDiscounts: () => apiClient.get<ItemDiscountRule[]>('/promos/item-discounts').then((r) => r.data),
  createItemDiscount: (body: ItemDiscountRequest) => apiClient.post<ItemDiscountRule>('/promos/item-discounts', body).then((r) => r.data),
  updateItemDiscount: (id: string, body: UpdateItemDiscountRequest) =>
    apiClient.put<ItemDiscountRule>(`/promos/item-discounts/${id}`, body).then((r) => r.data),

  listPromoCodes: () => apiClient.get<PromoCode[]>('/promo-codes').then((r) => r.data),
  createPromoCode: (body: CreatePromoCodeRequest) => apiClient.post<PromoCode>('/promo-codes', body).then((r) => r.data),
};
