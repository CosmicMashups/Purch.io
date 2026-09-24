import { apiClient } from '../../lib/apiClient';
import type { CreditLedger, CreditReminder } from './types';

export interface CreateCreditBody {
  customerFullName: string;
  customerPhoneNumber: string;
  customerAddress: string | null;
  creditLimit: number;
  /** `yyyy-MM-dd`, or null for no due date. */
  dueDate: string | null;
}

export const creditApi = {
  list: () => apiClient.get<CreditLedger[]>('/credit-ledger').then((r) => r.data),
  create: (body: CreateCreditBody) => apiClient.post<CreditLedger>('/credit-ledger', body).then((r) => r.data),
  recordPayment: (id: string, body: { amount: number; note: string | null }) =>
    apiClient.post<CreditLedger>(`/credit-ledger/${id}/payments`, body).then((r) => r.data),
  reminders: (withinDays: number) => apiClient.get<CreditReminder[]>('/credit-ledger/reminders', { params: { withinDays } }).then((r) => r.data),
  updateLimit: (id: string, body: { creditLimit: number; reason: string | null }) =>
    apiClient.put<CreditLedger>(`/credit-ledger/${id}/credit-limit`, body).then((r) => r.data),
  anonymize: (id: string) => apiClient.post<CreditLedger>(`/credit-ledger/${id}/anonymize`).then((r) => r.data),
};
