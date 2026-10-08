import { apiClient } from '../../lib/apiClient';

/** Every kind of record that can be made Inactive or deleted. Matches LifecycleKind on the server. */
export type LifecycleKind =
  | 'Item'
  | 'Ingredient'
  | 'Category'
  | 'Supplier'
  | 'ModifierGroup'
  | 'Modifier'
  | 'BogoPromo'
  | 'ComboPromo'
  | 'ItemDiscountPromo'
  | 'PromoCode'
  | 'Staff'
  | 'Branch'
  | 'Device'
  | 'Customer';

export type LifecycleStatus = 'Active' | 'Inactive' | 'Deleted';

export interface LifecycleImpact {
  id: string;
  name: string;
  status: LifecycleStatus;
  notes: string[];
  deactivateBlockedReason: string | null;
  deleteBlockedReason: string | null;
}

export interface LifecycleResult {
  id: string;
  name: string;
  status: LifecycleStatus;
}

export interface DeletedRecord {
  id: string;
  name: string;
  deletedAt: string | null;
}

export type LifecycleAction = 'deactivate' | 'reactivate' | 'delete' | 'restore';

export const lifecycleApi = {
  impact: async (kind: LifecycleKind, id: string) => (await apiClient.get<LifecycleImpact>(`/lifecycle/${kind}/${id}/impact`)).data,
  act: async (kind: LifecycleKind, id: string, action: LifecycleAction) =>
    (await apiClient.post<LifecycleResult>(`/lifecycle/${kind}/${id}/${action}`)).data,
  deleted: async (kind: LifecycleKind) => (await apiClient.get<DeletedRecord[]>(`/lifecycle/${kind}/deleted`)).data,
};
