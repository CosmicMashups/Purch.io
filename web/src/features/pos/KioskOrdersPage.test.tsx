import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { ApiError } from '../../lib/apiError';
import { makeCart, makeLine } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { posApi } from './api';
import { KioskOrdersPage } from './KioskOrdersPage';

vi.mock('./api', () => ({ posApi: { listKioskPending: vi.fn(), claimKioskOrder: vi.fn(), getCart: vi.fn() } }));

const order = (id: string, prep: number) =>
  makeCart({
    id,
    status: 1,
    originatedFromKiosk: true,
    kioskPrepNumber: prep,
    orderType: 'Dine In',
    lines: [makeLine({ id: `${id}-1`, itemName: 'Iced Latte', quantity: 2 }), makeLine({ id: `${id}-2`, itemName: 'Ube Cookie', quantity: 1 })],
    totalAmount: 360,
  });

function renderKiosk() {
  return renderPage(<KioskOrdersPage />, {
    route: '/sell/kiosk-orders',
    path: '/sell/kiosk-orders',
    otherRoutes: [{ path: '/sell', element: <p>Sell home</p> }],
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  signInAs('Cashier', { device_id: 'dev1', branch_id: 'kat' });
  vi.mocked(posApi.listKioskPending).mockResolvedValue([order('a', 12), order('b', 13)]);
});

describe('KioskOrdersPage', () => {
  it('needs a paired device', () => {
    signInAs('Admin');
    renderPage(<KioskOrdersPage />);
    expect(screen.getByText('Sign in with a device to sell')).toBeInTheDocument();
    expect(posApi.listKioskPending).not.toHaveBeenCalled();
  });

  it("asks for this device's own branch and lists each order with its number and the server total", async () => {
    renderKiosk();
    expect(await screen.findByText('Order 12')).toBeInTheDocument();
    expect(screen.getByText('Order 13')).toBeInTheDocument();
    expect(screen.getAllByText('2 x Iced Latte, 1 x Ube Cookie')).toHaveLength(2);
    expect(screen.getAllByText(/Dine In, total ₱360\.00/)).toHaveLength(2);
    expect(vi.mocked(posApi.listKioskPending).mock.calls[0][0]).toBe('kat');
  });

  it('says so when nothing is waiting', async () => {
    vi.mocked(posApi.listKioskPending).mockResolvedValue([]);
    renderKiosk();
    expect(await screen.findByText(/No kiosk orders are waiting/)).toBeInTheDocument();
  });

  it('claims an order and continues to the cart', async () => {
    vi.mocked(posApi.claimKioskOrder).mockResolvedValue({ ...order('a', 12), status: 0 });
    renderKiosk();
    fireEvent.click((await screen.findAllByRole('button', { name: 'Take this order' }))[0]);

    await waitFor(() => expect(posApi.claimKioskOrder).toHaveBeenCalledWith('a'));
    expect(await screen.findByText('Sell home')).toBeInTheDocument();
    expect(useToastStore.getState().toasts[0].message).toBe('Order 12 is now your cart');
  });

  it('stays put and shows the reason when the server refuses the claim', async () => {
    vi.mocked(posApi.claimKioskOrder).mockRejectedValue(new ApiError('validation', 'Finish or void your current cart before claiming a kiosk order.'));
    renderKiosk();
    fireEvent.click((await screen.findAllByRole('button', { name: 'Take this order' }))[1]);

    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toBe('Finish or void your current cart before claiming a kiosk order.'));
    expect(screen.queryByText('Sell home')).not.toBeInTheDocument();
    expect(screen.getByText('Order 13')).toBeInTheDocument();
  });

  it('shows a retryable error instead of an empty list when loading fails', async () => {
    vi.mocked(posApi.listKioskPending).mockRejectedValue(new Error('boom'));
    renderKiosk();
    expect(await screen.findByText('Kiosk orders could not be loaded')).toBeInTheDocument();
    expect(screen.queryByText(/No kiosk orders are waiting/)).not.toBeInTheDocument();
  });
});
