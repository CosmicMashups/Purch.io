import { apiClient } from '../../lib/apiClient';
import type { Branch, BranchDepartment } from './types';

export interface HardwareBody {
  receiptPrinterProfile: number;
  cashDrawerEnabled: boolean;
  cashDrawerPolicy: number;
}

export interface GcashBody {
  qrImageUrl: string | null;
  accountName: string | null;
  accountNumber: string | null;
}

export const branchAdminApi = {
  create: (body: { name: string; address: string | null }) => apiClient.post<Branch>('/branches', body).then((r) => r.data),
  updateHardware: (id: string, body: HardwareBody) => apiClient.put<Branch>(`/branches/${id}/hardware-settings`, body).then((r) => r.data),
  updateGcash: (id: string, body: GcashBody) => apiClient.put<Branch>(`/branches/${id}/manual-gcash-qr`, body).then((r) => r.data),
  listDepartments: (id: string) => apiClient.get<BranchDepartment[]>(`/branches/${id}/departments`).then((r) => r.data),
  createDepartment: (id: string, body: { name: string; concessionaireContactInfo: string | null }) =>
    apiClient.post<BranchDepartment>(`/branches/${id}/departments`, body).then((r) => r.data),
};
