import { describe, expect, it } from 'vitest';
import { availableCredit, canAnonymize, customerSchema, limitProblem, paymentProblem } from './rules';

const base = { customerFullName: 'Aling Nena', customerPhoneNumber: '0917 555 0101', customerAddress: '', creditLimit: 1000, dueDate: '' };

describe('customerSchema', () => {
  it('accepts a customer with no address or due date', () => {
    expect(customerSchema.safeParse(base).success).toBe(true);
  });

  it('needs a name, a phone number and a non-negative limit', () => {
    expect(customerSchema.safeParse({ ...base, customerFullName: ' ' }).success).toBe(false);
    expect(customerSchema.safeParse({ ...base, customerPhoneNumber: '' }).success).toBe(false);
    expect(customerSchema.safeParse({ ...base, creditLimit: -1 }).success).toBe(false);
    expect(customerSchema.safeParse({ ...base, creditLimit: NaN }).success).toBe(false);
  });

  it('accepts a zero limit and a real due date, rejects a malformed one', () => {
    expect(customerSchema.safeParse({ ...base, creditLimit: 0, dueDate: '2026-10-15' }).success).toBe(true);
    expect(customerSchema.safeParse({ ...base, dueDate: '15/10/2026' }).success).toBe(false);
  });
});

describe('availableCredit', () => {
  it('is the limit minus what is owed, never negative', () => {
    expect(availableCredit({ creditLimit: 1000, balance: 200 })).toBe(800);
    expect(availableCredit({ creditLimit: 100, balance: 250 })).toBe(0);
  });
});

describe('paymentProblem', () => {
  it('accepts up to the full balance', () => {
    expect(paymentProblem('200', 200)).toBeNull();
    expect(paymentProblem('50.5', 200)).toBeNull();
  });

  it('rejects blank, zero, negative, non-numeric and more than owed', () => {
    expect(paymentProblem('', 200)).toBe('Enter the amount paid');
    expect(paymentProblem('0', 200)).toBe('Enter an amount above zero');
    expect(paymentProblem('-5', 200)).toBe('Enter an amount above zero');
    expect(paymentProblem('abc', 200)).toBe('Enter an amount above zero');
    expect(paymentProblem('200.01', 200)).toBe('That is more than the customer owes');
  });
});

describe('limitProblem', () => {
  it('allows zero and rejects blank, negative and non-numeric', () => {
    expect(limitProblem('0')).toBeNull();
    expect(limitProblem('')).toBe('Enter the new limit');
    expect(limitProblem('-1')).toBe('The limit cannot be negative');
    expect(limitProblem('x')).toBe('Enter a number');
  });
});

describe('canAnonymize', () => {
  it('only when nothing is owed and the account is still active', () => {
    expect(canAnonymize({ balance: 0, isActive: true })).toBe(true);
    expect(canAnonymize({ balance: 10, isActive: true })).toBe(false);
    expect(canAnonymize({ balance: 0, isActive: false })).toBe(false);
  });
});
