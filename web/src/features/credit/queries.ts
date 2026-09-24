import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { creditApi, type CreateCreditBody } from './api';

export const creditKeys = { all: ['credit-ledger'] as const, reminders: ['credit-ledger', 'reminders'] as const };

export const useCreditLedgers = (enabled = true) => useQuery({ queryKey: creditKeys.all, queryFn: () => creditApi.list(), enabled });
export const useCreditReminders = (withinDays = 7) =>
  useQuery({ queryKey: [...creditKeys.reminders, withinDays], queryFn: () => creditApi.reminders(withinDays) });

/** Any change to an account can change what is due, so the list and the reminders refresh together. */
function useCreditMutation<TVars>(fn: (vars: TVars) => Promise<unknown>) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (vars: TVars) => fn(vars), onSuccess: () => qc.invalidateQueries({ queryKey: creditKeys.all }) });
}

export const useCreateCredit = () => useCreditMutation((body: CreateCreditBody) => creditApi.create(body));
export const useRecordCreditPayment = () =>
  useCreditMutation(({ id, amount, note }: { id: string; amount: number; note: string | null }) => creditApi.recordPayment(id, { amount, note }));
export const useUpdateCreditLimit = () =>
  useCreditMutation(({ id, creditLimit, reason }: { id: string; creditLimit: number; reason: string | null }) => creditApi.updateLimit(id, { creditLimit, reason }));
export const useAnonymizeCustomer = () => useCreditMutation((id: string) => creditApi.anonymize(id));
