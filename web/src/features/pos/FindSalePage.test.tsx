import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { makeCart, makeLine } from '../../test/pos';
import { renderPage } from '../../test/render';
import { posApi } from './api';
import { FindSalePage } from './FindSalePage';
import { ReceiptPage } from './ReceiptPage';

vi.mock('./api', () => ({ posApi: { findByReceiptNumber: vi.fn() } }));

function renderFindSale() {
  return renderPage(<FindSalePage />, {
    route: '/sell/find-sale',
    path: '/sell/find-sale',
    otherRoutes: [
      { path: '/sell/receipt', element: <ReceiptPage /> },
      { path: '/sell', element: <p>Sell home</p> },
    ],
  });
}

beforeEach(() => vi.clearAllMocks());

describe('FindSalePage', () => {
  it('rejects a non-numeric receipt number without calling the server', async () => {
    renderFindSale();
    fireEvent.change(screen.getByPlaceholderText('e.g. 1047'), { target: { value: 'abc' } });
    fireEvent.click(screen.getByRole('button', { name: 'Find sale' }));

    expect(await screen.findByText(/Enter the receipt number/)).toBeInTheDocument();
    expect(posApi.findByReceiptNumber).not.toHaveBeenCalled();
  });

  it('shows a not-found message when nothing matches', async () => {
    vi.mocked(posApi.findByReceiptNumber).mockResolvedValue([]);
    renderFindSale();
    fireEvent.change(screen.getByPlaceholderText('e.g. 1047'), { target: { value: '999' } });
    fireEvent.click(screen.getByRole('button', { name: 'Find sale' }));

    expect(await screen.findByText(/No completed sale found with receipt number 999/)).toBeInTheDocument();
  });

  it('goes straight to the receipt when exactly one sale matches', async () => {
    const sale = makeCart({ id: 'sale-1', receiptNumber: 1047, lines: [makeLine({ id: 'l1', itemName: 'Iced Latte', lineTotal: 150 })], totalAmount: 150 });
    vi.mocked(posApi.findByReceiptNumber).mockResolvedValue([sale]);
    renderFindSale();
    fireEvent.change(screen.getByPlaceholderText('e.g. 1047'), { target: { value: '1047' } });
    fireEvent.click(screen.getByRole('button', { name: 'Find sale' }));

    await waitFor(() => expect(posApi.findByReceiptNumber).toHaveBeenCalledWith(1047));
    expect(await screen.findByText('OR No. 00001047')).toBeInTheDocument();
  });

  it('lets the cashier pick when more than one sale shares a receipt number', async () => {
    const first = makeCart({ id: 'sale-1', receiptNumber: 7, lines: [makeLine({ id: 'l1', itemName: 'Iced Latte' })], totalAmount: 150 });
    const second = makeCart({ id: 'sale-2', receiptNumber: 7, lines: [makeLine({ id: 'l2', itemName: 'Mocha' })], totalAmount: 170 });
    vi.mocked(posApi.findByReceiptNumber).mockResolvedValue([first, second]);
    renderFindSale();
    fireEvent.change(screen.getByPlaceholderText('e.g. 1047'), { target: { value: '7' } });
    fireEvent.click(screen.getByRole('button', { name: 'Find sale' }));

    expect(await screen.findByText('Mocha')).toBeInTheDocument();
    fireEvent.click(screen.getByText('Mocha'));

    expect(await screen.findByText('OR No. 00000007')).toBeInTheDocument();
    expect(await screen.findByText(/170/)).toBeInTheDocument();
  });
});
