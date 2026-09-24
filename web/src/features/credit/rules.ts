import { z } from 'zod';

const num = (message: string) => z.number({ invalid_type_error: message, required_error: message });

export const customerSchema = z.object({
  customerFullName: z.string().trim().min(1, 'Enter the customer name'),
  customerPhoneNumber: z.string().trim().min(1, 'Enter a phone number'),
  customerAddress: z.string(),
  creditLimit: num('Enter a credit limit').min(0, 'The limit cannot be negative'),
  dueDate: z.string().refine((v) => v === '' || /^\d{4}-\d{2}-\d{2}$/.test(v), 'Choose a valid date'),
});

export type CustomerForm = z.infer<typeof customerSchema>;

/** What is still available to charge. The server enforces the limit; this only labels the account. */
export function availableCredit(ledger: { creditLimit: number; balance: number }): number {
  return Math.max(0, ledger.creditLimit - ledger.balance);
}

/** A repayment must be above zero and no more than what is owed, as the API requires. */
export function paymentProblem(amountText: string, balance: number): string | null {
  const trimmed = amountText.trim();
  if (trimmed === '') return 'Enter the amount paid';
  const amount = Number(trimmed);
  if (!Number.isFinite(amount) || amount <= 0) return 'Enter an amount above zero';
  if (amount > balance) return 'That is more than the customer owes';
  return null;
}

export function limitProblem(text: string): string | null {
  const trimmed = text.trim();
  if (trimmed === '') return 'Enter the new limit';
  const value = Number(trimmed);
  if (!Number.isFinite(value)) return 'Enter a number';
  if (value < 0) return 'The limit cannot be negative';
  return null;
}

/** Anonymizing erases the customer's details for good, so the API only allows it once nothing is owed. */
export function canAnonymize(ledger: { balance: number; isActive: boolean }): boolean {
  return ledger.balance === 0 && ledger.isActive;
}
