import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { makeCart, makeItem, makeLine } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { catalogApi } from '../catalog/api';
import { PricingType } from '../catalog/types';
import { posApi } from './api';
import { ExchangePage } from './ExchangePage';
import { usePosStore } from './posStore';
import { PaymentMethod, TransactionStatus, type Adjustment } from './types';

vi.mock('./api', () => ({ posApi: { exchange: vi.fn(), returnableLines: vi.fn() } }));
vi.mock('../catalog/api', () => ({ catalogApi: { listItems: vi.fn(), listVariants: vi.fn() } }));

const sale = makeCart({
  id: 'sale-1',
  status: TransactionStatus.Completed,
  receiptNumber: 1047,
  lines: [makeLine({ id: 'l1', itemName: 'Iced Latte', quantity: 2, unitPrice: 150, lineTotal: 300 })],
  totalAmount: 300,
});

function result(over: Partial<Adjustment> = {}): Adjustment {
  return {
    id: 'adj1',
    originalTransactionId: 'sale-1',
    originalReceiptNumber: 1047,
    createdAt: '2026-10-02T00:00:00Z',
    reason: 'Wrong drink',
    approvedByUserId: 'm1',
    approvedByName: 'Marisol',
    returnLines: [{ itemId: 'i', itemName: 'Iced Latte', itemVariantId: null, quantity: 1, unitPrice: 150, lineTotal: 150 }],
    replacementLines: [{ itemId: 'mocha', itemName: 'Mocha', itemVariantId: null, quantity: 1, unitPrice: 170, lineTotal: 170 }],
    returnedTotal: 150,
    replacementTotal: 170,
    priceDifference: 20,
    settlementMethod: PaymentMethod.Cash,
    changeGiven: 180,
    ...over,
  };
}

function renderExchange() {
  return renderPage(<ExchangePage />, { route: '/sell/exchange', path: '/sell/exchange', otherRoutes: [{ path: '/sell', element: <p>Sell home</p> }] });
}

function fill(label: string | RegExp, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

async function pickMocha() {
  fireEvent.change(screen.getByPlaceholderText('Name, SKU or barcode'), { target: { value: 'moc' } });
  fireEvent.click(await screen.findByRole('button', { name: /Mocha/ }));
}

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Cashier', { device_id: 'dev1', branch_id: 'kat', scope_type: 'Branch', scope_id: 'kat' });
  usePosStore.getState().showReceipt(sale);
  vi.mocked(catalogApi.listItems).mockResolvedValue([
    makeItem({ id: 'mocha', name: 'Mocha', basePrice: 170 }),
    makeItem({ id: 'combo', name: 'Mocha Combo', pricingType: PricingType.Combo }),
  ]);
  vi.mocked(catalogApi.listVariants).mockResolvedValue([]);
  vi.mocked(posApi.returnableLines).mockResolvedValue([{ lineId: 'l1', remainingQuantity: 2 }]);
});

describe('ExchangePage', () => {
  it('caps the return quantity at what earlier exchanges left', async () => {
    vi.mocked(posApi.returnableLines).mockResolvedValue([{ lineId: 'l1', remainingQuantity: 1 }]);
    renderExchange();

    expect(await screen.findByText(/1 left to return/)).toBeInTheDocument();
    const group = screen.getByRole('group', { name: 'Return quantity for Iced Latte' });
    fireEvent.click(within(group).getByRole('button', { name: 'Increase' }));
    expect(within(group).getByRole('button', { name: 'Increase' })).toBeDisabled();
  });

  it('only offers items an exchange supports', async () => {
    renderExchange();
    fireEvent.change(screen.getByPlaceholderText('Name, SKU or barcode'), { target: { value: 'moc' } });

    expect(await screen.findByRole('button', { name: /^Mocha/ })).toBeInTheDocument();
    expect(screen.queryByText('Mocha Combo')).not.toBeInTheDocument();
  });

  it('previews what the customer pays and sends the exchange with cash tendered', async () => {
    vi.mocked(posApi.exchange).mockResolvedValue(result());
    renderExchange();

    fireEvent.click(within(screen.getByRole('group', { name: 'Return quantity for Iced Latte' })).getByRole('button', { name: 'Increase' }));
    await pickMocha();

    expect(screen.getByText('Customer pays')).toBeInTheDocument();
    fill('Cash received', '200');
    expect(screen.getByText(/Change: .*180/)).toBeInTheDocument();
    fill('Reason', 'Wrong drink');
    fill('Manager or admin PIN', '1234');
    fireEvent.click(screen.getByRole('button', { name: 'Record exchange' }));

    await waitFor(() =>
      expect(posApi.exchange).toHaveBeenCalledWith('sale-1', {
        returnLines: [{ originalLineId: 'l1', quantity: 1 }],
        replacementLines: [{ itemId: 'mocha', itemVariantId: null, quantity: 1 }],
        reason: 'Wrong drink',
        approverPin: '1234',
        settlementMethod: PaymentMethod.Cash,
        settlementAmountTendered: 200,
      }),
    );
    expect(await screen.findByText('Exchange recorded')).toBeInTheDocument();
    expect(screen.getByText(/approved by Marisol/)).toBeInTheDocument();
  });

  it('blocks recording until cash covers the amount owed', async () => {
    renderExchange();
    fireEvent.click(within(screen.getByRole('group', { name: 'Return quantity for Iced Latte' })).getByRole('button', { name: 'Increase' }));
    await pickMocha();
    fill('Cash received', '10');
    fill('Reason', 'Wrong drink');
    fill('Manager or admin PIN', '1234');

    expect(screen.getByRole('button', { name: 'Record exchange' })).toBeDisabled();
  });

  it("keeps the form filled in and shows the server's message when it refuses", async () => {
    vi.mocked(posApi.exchange).mockRejectedValue(new ApiError('validation', 'Only 0 of that line is still available to return (the rest has already been returned).'));
    renderExchange();
    fireEvent.click(within(screen.getByRole('group', { name: 'Return quantity for Iced Latte' })).getByRole('button', { name: 'Increase' }));
    await pickMocha();
    fill('Cash received', '200');
    fill('Reason', 'Wrong drink');
    fill('Manager or admin PIN', '1234');
    fireEvent.click(screen.getByRole('button', { name: 'Record exchange' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/still available to return/);
    expect(screen.getByLabelText('Reason')).toHaveValue('Wrong drink');
  });
});

