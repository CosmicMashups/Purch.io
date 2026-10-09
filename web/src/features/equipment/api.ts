import { apiClient } from '../../lib/apiClient';
import type { CreateEquipmentRequest, Equipment, EquipmentStatus, ItemEquipment, UpdateEquipmentRequest } from './types';

export const equipmentApi = {
  list: () => apiClient.get<Equipment[]>('/equipment').then((r) => r.data),
  create: (body: CreateEquipmentRequest) => apiClient.post<Equipment>('/equipment', body).then((r) => r.data),
  update: (id: string, body: UpdateEquipmentRequest) => apiClient.put<Equipment>(`/equipment/${id}`, body).then((r) => r.data),
  setStatus: (id: string, status: EquipmentStatus) => apiClient.put<Equipment>(`/equipment/${id}/status`, { status }).then((r) => r.data),
  reorder: (equipmentIds: string[]) => apiClient.put('/equipment/order', { equipmentIds }).then(() => undefined),

  getItemEquipment: (itemId: string) => apiClient.get<ItemEquipment[]>(`/items/${itemId}/equipment`).then((r) => r.data),
  replaceItemEquipment: (itemId: string, equipmentIds: string[]) =>
    apiClient.put<ItemEquipment[]>(`/items/${itemId}/equipment`, { equipmentIds }).then((r) => r.data),
};
