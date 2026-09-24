import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { promotionsApi } from './api';
import type {
  BogoRequest,
  ComboRequest,
  CreatePromoCodeRequest,
  ItemDiscountRequest,
  UpdateBogoRequest,
  UpdateComboRequest,
  UpdateItemDiscountRequest,
} from './types';

export const promoKeys = {
  bogo: ['promos', 'bogo'] as const,
  combos: ['promos', 'combos'] as const,
  itemDiscounts: ['promos', 'item-discounts'] as const,
  codes: ['promos', 'codes'] as const,
};

export const useBogoRules = () => useQuery({ queryKey: promoKeys.bogo, queryFn: promotionsApi.listBogo });
export const useComboRules = () => useQuery({ queryKey: promoKeys.combos, queryFn: promotionsApi.listCombos });
export const useItemDiscountRules = () => useQuery({ queryKey: promoKeys.itemDiscounts, queryFn: promotionsApi.listItemDiscounts });
export const usePromoCodes = () => useQuery({ queryKey: promoKeys.codes, queryFn: promotionsApi.listPromoCodes });

function useSaver<TVars>(key: readonly string[], fn: (vars: TVars) => Promise<unknown>) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => qc.invalidateQueries({ queryKey: key }) });
}

export const useCreateBogo = () => useSaver(promoKeys.bogo, (body: BogoRequest) => promotionsApi.createBogo(body));
export const useUpdateBogo = () =>
  useSaver(promoKeys.bogo, ({ id, body }: { id: string; body: UpdateBogoRequest }) => promotionsApi.updateBogo(id, body));

export const useCreateCombo = () => useSaver(promoKeys.combos, (body: ComboRequest) => promotionsApi.createCombo(body));
export const useUpdateCombo = () =>
  useSaver(promoKeys.combos, ({ id, body }: { id: string; body: UpdateComboRequest }) => promotionsApi.updateCombo(id, body));

export const useCreateItemDiscount = () =>
  useSaver(promoKeys.itemDiscounts, (body: ItemDiscountRequest) => promotionsApi.createItemDiscount(body));
export const useUpdateItemDiscount = () =>
  useSaver(promoKeys.itemDiscounts, ({ id, body }: { id: string; body: UpdateItemDiscountRequest }) =>
    promotionsApi.updateItemDiscount(id, body),
  );

export const useCreatePromoCode = () =>
  useSaver(promoKeys.codes, (body: CreatePromoCodeRequest) => promotionsApi.createPromoCode(body));
