import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { ApiError } from '../../lib/apiError';
import { chooseFromMenu } from '../../test/menu';
import { renderPage, signInAs } from '../../test/render';
import { creditApi } from './api';
import { CustomersPage } from './CustomersPage';
import type { CreditLedger } from './types';

vi.mock('./api', () => ({ creditApi: { list: vi.fn(), create: vi.fn(), recordPayment: vi.fn(), reminders: vi.fn(), updateLimit: vi.fn(), anonymize: vi.fn() } }));

const nena: CreditLedger = { id: 'c1', customerFullName: 'Aling Nena', customerPhoneNumber: '0917 555 0101', customerAddress: null, balance: 200, creditLimit: 1000, dueDate: '2026-10-01', isActive: true };
const paid: CreditLedger = { id: 'c2', customerFullName: 'Mang Ben', customerPhoneNumber: '0918', customerAddress: null, balance: 0, creditLimit: 500, dueDate: null, isActive: true };
const erased: CreditLedger = { ...paid, id: 'c3', customerFullName: '[ANONYMIZED]', customerPhoneNumber: '00000000000', isActive: false };
const resting: CreditLedger = { ...nena, id: 'c4', customerFullName: 'Kuya Resty', balance: 0, isActive: false };

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  signInAs('Manager');
  vi.mocked(creditApi.list).mockResolvedValue([nena, paid, erased, resting]);
  vi.mocked(creditApi.reminders).mockResolvedValue([{ id: 'c1', customerFullName: 'Aling Nena', customerPhoneNumber: '0917 555 0101', balance: 200, dueDate: '2026-10-01', isOverdue: true }]);
  vi.mocked(creditApi.create).mockResolvedValue(nena);
  vi.mocked(creditApi.recordPayment).mockResolvedValue(nena);
  vi.mocked(creditApi.updateLimit).mockResolvedValue(nena);
  vi.mocked(creditApi.anonymize).mockResolvedValue(erased);
});

// A customer can also appear in the reminders panel, so pick the account card (the one that shows a balance).
const card = async (name: string) => {
  const matches = await screen.findAllByText(name);
  return matches.map((el) => el.closest('li') as HTMLElement).find((li) => li.textContent?.includes('Owes'))!;
};

describe('CustomersPage list', () => {
  it('shows balances, limits and what is available, plus who is overdue', async () => {
    renderPage(<CustomersPage />);
    expect(await screen.findByText('Owes ₱200.00 of ₱1,000.00 (₱800.00 available)')).toBeInTheDocument();
    expect(await screen.findByText('Overdue')).toBeInTheDocument();
  });

  it('only offers payment when something is owed, and erasing only at a zero balance', async () => {
    renderPage(<CustomersPage />);
    await card('Aling Nena');
    fireEvent.click(screen.getByRole('button', { name: 'Actions for Aling Nena' }));
    expect(screen.getByRole('menuitem', { name: /^Record payment/ })).toBeEnabled();
    expect(screen.getByRole('menuitem', { name: /^Erase details for good/ })).toBeDisabled();
    // Deleting is held back while money is owed; going inactive is not.
    expect(screen.getByRole('menuitem', { name: /^Delete/ })).toBeDisabled();
    expect(screen.getByRole('menuitem', { name: /^Make inactive/ })).toBeEnabled();
    fireEvent.keyDown(window, { key: 'Escape' });

    fireEvent.click(screen.getByRole('button', { name: 'Actions for Mang Ben' }));
    expect(screen.getByRole('menuitem', { name: /^Record payment/ })).toBeDisabled();
    expect(screen.getByRole('menuitem', { name: /^Erase details for good/ })).toBeEnabled();
    expect(screen.getByRole('menuitem', { name: /^Delete/ })).toBeEnabled();
  });

  it('keeps inactive customers apart, marks an erased one, and gives it no actions', async () => {
    renderPage(<CustomersPage />);
    await card('Aling Nena');
    expect(screen.queryByText('Kuya Resty')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Inactive' }));
    const resty = await card('Kuya Resty');
    expect(within(resty).getByText('Inactive')).toBeInTheDocument();
    expect(within(resty).getByText(/No new credit/)).toBeInTheDocument();
    const c = await card('[ANONYMIZED]');
    expect(within(c).getByText('Erased')).toBeInTheDocument();
    expect(within(c).queryByRole('button', { name: /^Actions for/ })).not.toBeInTheDocument();
  });

  it('shows a retryable error instead of an empty list', async () => {
    vi.mocked(creditApi.list).mockRejectedValue(new Error('boom'));
    renderPage(<CustomersPage />);
    expect(await screen.findByText('Customers could not be loaded')).toBeInTheDocument();
  });
});

describe('adding a customer', () => {
  it('sends blanks as null and the due date as a plain date', async () => {
    renderPage(<CustomersPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Add customer' }));
    fireEvent.change(await screen.findByLabelText('Full name'), { target: { value: 'Ate Rose' } });
    fireEvent.change(screen.getByLabelText('Phone number'), { target: { value: '0920 111 2222' } });
    fireEvent.change(screen.getByLabelText('Credit limit (PHP)'), { target: { value: '750' } });
    fireEvent.change(screen.getByLabelText('Due date (optional)'), { target: { value: '2026-11-05' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(creditApi.create).toHaveBeenCalledWith({ customerFullName: 'Ate Rose', customerPhoneNumber: '0920 111 2222', customerAddress: null, creditLimit: 750, dueDate: '2026-11-05' }));
  });

  it('needs a name, a phone and a limit', async () => {
    renderPage(<CustomersPage />);
    await card('Aling Nena');
    fireEvent.click(screen.getByRole('button', { name: 'Add customer' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Enter the customer name')).toBeInTheDocument();
    expect(screen.getByText('Enter a phone number')).toBeInTheDocument();
    expect(screen.getByText('Enter a credit limit')).toBeInTheDocument();
    expect(creditApi.create).not.toHaveBeenCalled();
  });
});

describe('payments, limits and erasing', () => {
  it('records a repayment no larger than the balance', async () => {
    renderPage(<CustomersPage />);
    await card('Aling Nena');
    await chooseFromMenu('Aling Nena', 'Record payment');
    const dialog = await screen.findByRole('dialog', { name: 'Payment from Aling Nena' });

    fireEvent.change(within(dialog).getByLabelText('Amount paid (PHP)'), { target: { value: '500' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Record payment' }));
    expect(await within(dialog).findByText('That is more than the customer owes')).toBeInTheDocument();
    expect(creditApi.recordPayment).not.toHaveBeenCalled();

    fireEvent.change(within(dialog).getByLabelText('Amount paid (PHP)'), { target: { value: '150' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Record payment' }));
    await waitFor(() => expect(creditApi.recordPayment).toHaveBeenCalledWith('c1', { amount: 150, note: null }));
  });

  it('changes a limit, preloaded with the current one, with an optional reason', async () => {
    renderPage(<CustomersPage />);
    await card('Aling Nena');
    await chooseFromMenu('Aling Nena', 'Change limit');
    const dialog = await screen.findByRole('dialog', { name: 'Credit limit for Aling Nena' });
    expect(within(dialog).getByLabelText('New credit limit (PHP)')).toHaveValue('1000');
    fireEvent.change(within(dialog).getByLabelText('New credit limit (PHP)'), { target: { value: '1500' } });
    fireEvent.change(within(dialog).getByLabelText('Reason (optional)'), { target: { value: 'Good payer' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save limit' }));
    await waitFor(() => expect(creditApi.updateLimit).toHaveBeenCalledWith('c1', { creditLimit: 1500, reason: 'Good payer' }));
  });

  it('confirms before erasing a customer, and backing out changes nothing', async () => {
    renderPage(<CustomersPage />);
    await card('Mang Ben');
    await chooseFromMenu('Mang Ben', 'Erase details for good');
    const dialog = screen.getByRole('dialog', { name: "Erase this customer's details?" });
    expect(within(dialog).getByText(/cannot be undone/)).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(creditApi.anonymize).not.toHaveBeenCalled();

    await chooseFromMenu('Mang Ben', 'Erase details for good');
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Erase details' }));
    await waitFor(() => expect(creditApi.anonymize).toHaveBeenCalledWith('c2'));
  });

  it('shows the server message when a save is refused', async () => {
    vi.mocked(creditApi.recordPayment).mockRejectedValue(new ApiError('validation', "Payment amount can't exceed the customer's outstanding balance."));
    renderPage(<CustomersPage />);
    await card('Aling Nena');
    await chooseFromMenu('Aling Nena', 'Record payment');
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('Amount paid (PHP)'), { target: { value: '100' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Record payment' }));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toMatch(/can't exceed/));
  });
});
