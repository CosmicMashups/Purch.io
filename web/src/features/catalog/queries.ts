import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { catalogApi } from './api';
import { departmentsApi } from '../departments/api';
import type {
  AttachModifierGroupRequest,
  CreateBundlePromoRuleRequest,
  CreateCategoryRequest,
  CreateItemBatchRequest,
  CreateItemComboComponentRequest,
  CreateItemModifierRequest,
  CreateItemRequest,
  CreateItemVariantRequest,
  CreateModifierGroupRequest,
  ReplaceItemRecipeRequest,
  UpdateCategoryRequest,
  UpdateItemDepartmentRequest,
  UpdateItemRequest,
  UpdateLowStockThresholdRequest,
  UpdateServiceDurationRequest,
  UpdateTingiConfigRequest,
} from './types';

export const catalogKeys = {
  categories: ['categories'] as const,
  items: ['items'] as const,
  modifierGroups: ['modifierGroups'] as const,
  itemBatches: (itemId: string) => ['items', itemId, 'batches'] as const,
  bundleRules: (itemId: string) => ['items', itemId, 'bundleRules'] as const,
  variants: (itemId: string) => ['items', itemId, 'variants'] as const,
  itemModifierGroups: (itemId: string) => ['items', itemId, 'modifierGroups'] as const,
  comboComponents: (itemId: string) => ['items', itemId, 'comboComponents'] as const,
  recipe: (itemId: string) => ['items', itemId, 'recipe'] as const,
  departments: ['departments'] as const,
};

export function useCategories() {
  return useQuery({ queryKey: catalogKeys.categories, queryFn: catalogApi.listCategories });
}

export function useCreateCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateCategoryRequest) => catalogApi.createCategory(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.categories }),
  });
}

export function useUpdateCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ categoryId, body }: { categoryId: string; body: UpdateCategoryRequest }) =>
      catalogApi.updateCategory(categoryId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.categories }),
  });
}

export function useItems() {
  return useQuery({ queryKey: catalogKeys.items, queryFn: catalogApi.listItems });
}

export function useCreateItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateItemRequest) => catalogApi.createItem(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.items }),
  });
}

export function useUpdateItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ itemId, body }: { itemId: string; body: UpdateItemRequest }) =>
      catalogApi.updateItem(itemId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.items }),
  });
}

export function useModifierGroups() {
  return useQuery({ queryKey: catalogKeys.modifierGroups, queryFn: catalogApi.listModifierGroups });
}

export function useCreateModifierGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateModifierGroupRequest) => catalogApi.createModifierGroup(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.modifierGroups }),
  });
}

export function useAddModifier(groupId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateItemModifierRequest) => catalogApi.addModifier(groupId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.modifierGroups }),
  });
}

export function useItemModifierGroups(itemId: string) {
  return useQuery({
    queryKey: catalogKeys.itemModifierGroups(itemId),
    queryFn: () => catalogApi.listItemModifierGroups(itemId),
    enabled: !!itemId,
  });
}

export function useAttachModifierGroup(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: AttachModifierGroupRequest) => catalogApi.attachModifierGroup(itemId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.itemModifierGroups(itemId) }),
  });
}

export function useItemBatches(itemId: string) {
  return useQuery({
    queryKey: catalogKeys.itemBatches(itemId),
    queryFn: () => catalogApi.listBatches(itemId),
    enabled: !!itemId,
  });
}

export function useReceiveBatch(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateItemBatchRequest) => catalogApi.receiveBatch(itemId, body),
    onSuccess: () => {
      // Receiving stock changes Item.stockOnHand too.
      qc.invalidateQueries({ queryKey: catalogKeys.itemBatches(itemId) });
      qc.invalidateQueries({ queryKey: catalogKeys.items });
    },
  });
}

export function useBundleRules(itemId: string) {
  return useQuery({
    queryKey: catalogKeys.bundleRules(itemId),
    queryFn: () => catalogApi.listBundleRules(itemId),
    enabled: !!itemId,
  });
}

export function useCreateBundleRule(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateBundlePromoRuleRequest) => catalogApi.createBundleRule(itemId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.bundleRules(itemId) }),
  });
}

export function useVariants(itemId: string) {
  return useQuery({
    queryKey: catalogKeys.variants(itemId),
    queryFn: () => catalogApi.listVariants(itemId),
    enabled: !!itemId,
  });
}

export function useCreateVariant(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateItemVariantRequest) => catalogApi.createVariant(itemId, body),
    onSuccess: () => {
      // Adding a variant changes stock tracking too.
      qc.invalidateQueries({ queryKey: catalogKeys.variants(itemId) });
      qc.invalidateQueries({ queryKey: catalogKeys.items });
    },
  });
}

export function useComboComponents(itemId: string) {
  return useQuery({
    queryKey: catalogKeys.comboComponents(itemId),
    queryFn: () => catalogApi.listComboComponents(itemId),
    enabled: !!itemId,
  });
}

export function useCreateComboComponent(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateItemComboComponentRequest) => catalogApi.createComboComponent(itemId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.comboComponents(itemId) }),
  });
}

export function useUpdateTingiConfig(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateTingiConfigRequest) => catalogApi.updateTingiConfig(itemId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.items }),
  });
}

export function useUpdateServiceDuration(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateServiceDurationRequest) => catalogApi.updateServiceDuration(itemId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.items }),
  });
}

export function useUpdateItemDepartment(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateItemDepartmentRequest) => catalogApi.updateItemDepartment(itemId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.items }),
  });
}

export function useUpdateLowStockThreshold(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateLowStockThresholdRequest) => catalogApi.updateLowStockThreshold(itemId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogKeys.items }),
  });
}

export function useDepartments() {
  return useQuery({ queryKey: catalogKeys.departments, queryFn: departmentsApi.listAllDepartments });
}

export function useRecipe(itemId: string) {
  return useQuery({ queryKey: catalogKeys.recipe(itemId), queryFn: () => catalogApi.getRecipe(itemId), enabled: !!itemId });
}

export function useReplaceRecipe(itemId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: ReplaceItemRecipeRequest) => catalogApi.replaceRecipe(itemId, body),
    // Saving a recipe can retire or recreate the item's own stock record, so stock views are stale too.
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: catalogKeys.recipe(itemId) });
      await qc.invalidateQueries({ queryKey: ['inventory-items'] });
      await qc.invalidateQueries({ queryKey: catalogKeys.items });
    },
  });
}
