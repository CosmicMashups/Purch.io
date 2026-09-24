import { apiClient } from '../../lib/apiClient';
import type {
  AttachModifierGroupRequest,
  BundlePromoRule,
  Category,
  CreateBundlePromoRuleRequest,
  CreateCategoryRequest,
  CreateItemBatchRequest,
  CreateItemComboComponentRequest,
  CreateItemModifierRequest,
  CreateItemRequest,
  CreateItemVariantRequest,
  CreateModifierGroupRequest,
  Item,
  ItemBatch,
  ItemRecipeLine,
  ReplaceItemRecipeRequest,
  ItemComboComponent,
  ItemVariant,
  ModifierGroup,
  UpdateCategoryRequest,
  UpdateItemDepartmentRequest,
  UpdateItemRequest,
  UpdateLowStockThresholdRequest,
  UpdateServiceDurationRequest,
  UpdateTingiConfigRequest,
} from './types';

export const catalogApi = {
  listCategories: () => apiClient.get<Category[]>('/categories').then((r) => r.data),
  createCategory: (body: CreateCategoryRequest) =>
    apiClient.post<Category>('/categories', body).then((r) => r.data),
  updateCategory: (categoryId: string, body: UpdateCategoryRequest) =>
    apiClient.put<Category>(`/categories/${categoryId}`, body).then((r) => r.data),

  listItems: () => apiClient.get<Item[]>('/items').then((r) => r.data),
  createItem: (body: CreateItemRequest) => apiClient.post<Item>('/items', body).then((r) => r.data),
  updateItem: (itemId: string, body: UpdateItemRequest) =>
    apiClient.put<Item>(`/items/${itemId}`, body).then((r) => r.data),

  listModifierGroups: () => apiClient.get<ModifierGroup[]>('/modifier-groups').then((r) => r.data),
  createModifierGroup: (body: CreateModifierGroupRequest) =>
    apiClient.post<ModifierGroup>('/modifier-groups', body).then((r) => r.data),
  addModifier: (groupId: string, body: CreateItemModifierRequest) =>
    apiClient.post<ModifierGroup>(`/modifier-groups/${groupId}/modifiers`, body).then((r) => r.data),

  listItemModifierGroups: (itemId: string) =>
    apiClient.get<ModifierGroup[]>(`/items/${itemId}/modifier-groups`).then((r) => r.data),
  attachModifierGroup: (itemId: string, body: AttachModifierGroupRequest) =>
    apiClient.post<ModifierGroup>(`/items/${itemId}/modifier-groups`, body).then((r) => r.data),

  listBatches: (itemId: string) => apiClient.get<ItemBatch[]>(`/items/${itemId}/batches`).then((r) => r.data),
  receiveBatch: (itemId: string, body: CreateItemBatchRequest) =>
    apiClient.post<ItemBatch>(`/items/${itemId}/batches`, body).then((r) => r.data),

  listBundleRules: (itemId: string) =>
    apiClient.get<BundlePromoRule[]>(`/items/${itemId}/bundle-rules`).then((r) => r.data),
  createBundleRule: (itemId: string, body: CreateBundlePromoRuleRequest) =>
    apiClient.post<BundlePromoRule>(`/items/${itemId}/bundle-rules`, body).then((r) => r.data),

  listVariants: (itemId: string) =>
    apiClient.get<ItemVariant[]>(`/items/${itemId}/variants`).then((r) => r.data),
  createVariant: (itemId: string, body: CreateItemVariantRequest) =>
    apiClient.post<ItemVariant>(`/items/${itemId}/variants`, body).then((r) => r.data),

  listComboComponents: (itemId: string) =>
    apiClient.get<ItemComboComponent[]>(`/items/${itemId}/combo-components`).then((r) => r.data),
  createComboComponent: (itemId: string, body: CreateItemComboComponentRequest) =>
    apiClient.post<ItemComboComponent>(`/items/${itemId}/combo-components`, body).then((r) => r.data),

  updateTingiConfig: (itemId: string, body: UpdateTingiConfigRequest) =>
    apiClient.put<Item>(`/items/${itemId}/tingi-config`, body).then((r) => r.data),
  updateServiceDuration: (itemId: string, body: UpdateServiceDurationRequest) =>
    apiClient.put<Item>(`/items/${itemId}/service-duration`, body).then((r) => r.data),
  updateItemDepartment: (itemId: string, body: UpdateItemDepartmentRequest) =>
    apiClient.put<Item>(`/items/${itemId}/department`, body).then((r) => r.data),
  updateLowStockThreshold: (itemId: string, body: UpdateLowStockThresholdRequest) =>
    apiClient.put<Item>(`/items/${itemId}/low-stock-threshold`, body).then((r) => r.data),

  getRecipe: (itemId: string) => apiClient.get<ItemRecipeLine[]>(`/items/${itemId}/recipe`).then((r) => r.data),
  replaceRecipe: (itemId: string, body: ReplaceItemRecipeRequest) =>
    apiClient.put<ItemRecipeLine[]>(`/items/${itemId}/recipe`, body).then((r) => r.data),
};
