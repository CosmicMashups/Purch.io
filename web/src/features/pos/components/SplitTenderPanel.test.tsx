import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { creditApi } from '../../credit/api';
import { SplitTenderPanel } from './SplitTenderPanel';

vi.mock('../../credit/api', () => ({ creditApi: { list: vi.fn() } }));

const onComplete = vi.fn();
const onCancel = vi.fn();

function renderPanel(total = 300, extra: Partial<Parameters<typeof SplitTenderPanel>[0]> = {}) {
  return renderPage(<SplitTenderPanel total={total} creditOn busy={false} error={null} onComplete={onComplete} onCancel={onCancel} {...extra} />);
}

const amountField = () => screen.getByLabelText('Amount for this part');
const chip = (name: string) => within(screen.getByRole('radiogroup', { name: 'Method for this part' })).getByRole('radio', { name });

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Cashier', { device_id: 'dev1', branch_id: 'kat' });
  vi.mocked(creditApi.list).mockResolvedValue([
    { id: 'c1', customerFullName: 'Aling Nena', customerPhoneNumber: '0917', customerAddress: null, balance: 900, creditLimit: 1000, dueDate: null, isActive: true },
  ]);
});

describe('SplitTenderPanel', () => {
  it('prefills the amount with what is left and enables Complete only once nothing remains', () => {
    renderPanel();
    expect(screen.getByTestId('split-remaining')).toHaveTextContent('₱300.00');
    expect(amountField()).toHaveValue('300');
    const complete = screen.getByRole('button', { name: /Complete sale/ });
    expect(complete).toBeDisabled();

    fireEvent.change(amountField(), { target: { value: '100' } });
    fireEvent.change(screen.getByLabelText('Cash received'), { target: { value: '100' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add payment' }));
    expect(screen.getByTestId('split-remaining')).toHaveTextContent('₱200.00');
    expect(complete).toBeDisabled();

    fireEvent.click(chip('GCash'));
    expect(amountField()).toHaveValue('200');
    fireEvent.change(screen.getByLabelText('Reference (optional)'), { target: { value: 'GC-778812' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add payment' }));
    expect(screen.getByTestId('split-remaining')).toHaveTextContent('₱0.00');
    expect(complete).toBeEnabled();

    fireEvent.click(complete);
    expect(onComplete).toHaveBeenCalledWith([
      { method: 0, amount: 100, amountTendered: 100, customerCreditLedgerId: null, reference: null },
      { method: 3, amount: 200, amountTendered: null, customerCreditLedgerId: null, reference: 'GC-778812' },
    ]);
  });

  it('shows the change when cash received is more than the cash part', () => {
    renderPanel();
    fireEvent.change(amountField(), { target: { value: '100' } });
    fireEvent.change(screen.getByLabelText('Cash received'), { target: { value: '150' } });
    expect(screen.getByText('Change: ₱50.00')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Add payment' }));
    expect(screen.getByTestId('split-change')).toHaveTextContent('₱50.00');
  });

  it('will not take a non-cash part bigger than what is left', () => {
    renderPanel();
    fireEvent.click(chip('GCash'));
    fireEvent.change(amountField(), { target: { value: '400' } });
    expect(screen.getByText(/Only cash can be more than what's left/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add payment' })).toBeDisabled();
  });

  it('allows only one Utang part', async () => {
    renderPanel();
    fireEvent.click(chip('Utang / Credit'));
    fireEvent.change(amountField(), { target: { value: '50' } });
    fireEvent.change(await screen.findByLabelText('Customer account'), { target: { value: 'c1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add payment' }));
    expect(chip('Utang / Credit')).toBeDisabled();
    expect(screen.getByText(/Only one Utang part per sale/)).toBeInTheDocument();
  });

  it('needs a customer account for Utang and warns past the available credit, like the single flow', async () => {
    renderPanel();
    fireEvent.click(chip('Utang / Credit'));
    fireEvent.change(amountField(), { target: { value: '150' } });
    const add = screen.getByRole('button', { name: 'Add payment' });
    expect(add).toBeDisabled();
    expect(await screen.findByRole('option', { name: 'Aling Nena (₱100.00 available)' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Customer account'), { target: { value: 'c1' } });
    expect(screen.getByText(/more than the ₱100\.00 available/)).toBeInTheDocument();
    expect(add).toBeEnabled();
  });

  it('removes a part and gives its amount back to Remaining', () => {
    renderPanel();
    fireEvent.change(amountField(), { target: { value: '100' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add payment' }));
    fireEvent.click(screen.getByRole('button', { name: 'Remove Cash ₱100.00' }));
    expect(screen.getByTestId('split-remaining')).toHaveTextContent('₱300.00');
  });

  it('shows a server refusal inline', async () => {
    renderPanel(300, { error: 'The cart changed, review the total' });
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('The cart changed, review the total'));
  });
});
