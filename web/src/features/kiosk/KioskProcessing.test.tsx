import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { makeCart } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { kioskApi } from './api';
import { KioskProcessingPage } from './KioskProcessingPage';
import { useKioskStore } from './kioskStore';
import { useLocalKioskCartStore } from './localCart';

vi.mock('./api', () => ({
  deviceApi: { pair: vi.fn() },
  kioskApi: { placeOrder: vi.fn(), branding: vi.fn(), promoRules: vi.fn() },
  displayApi: { pending: vi.fn(), setKitchenStatus: vi.fn() },
}));

const other = [
  { path: '/kiosk/menu', element: <p>Menu page</p> },
  { path: '/kiosk/cart', element: <p>Cart page</p> },
  { path: '/kiosk/payment', element: <p>Payment page</p> },
  { path: '/kiosk/done', element: <p>Done page</p> },
];
const route = { route: '/kiosk/processing', path: '/kiosk/processing', otherRoutes: other };

const line = { localId: 'l1', itemId: 'i1', itemName: 'Iced Latte', itemVariantId: null, itemVariantAttributes: {}, quantity: 2, unitPrice: 150, comboSelections: [], modifierSelections: [] };

beforeEach(() => {
  vi.clearAllMocks();
  window.sessionStorage.clear();
  signInAs('Kiosk', { device_id: 'k1', branch_id: 'b1' });
  useKioskStore.setState({ submitted: null });
  useKioskStore.getState().resetCheckout();
  useKioskStore.getState().setOrderType('Take Out');
  useKioskStore.getState().setPayment('discount', 'senior');
  useLocalKioskCartStore.setState({ lines: [line], lastActivityAt: Date.now() });
});

describe('KioskProcessingPage', () => {
  it('sends the whole order, payment choice included, in one call and then shows the confirmation', async () => {
    vi.mocked(kioskApi.placeOrder).mockResolvedValue(makeCart({ kioskPrepNumber: 42, orderType: 'Take Out' }));
    renderPage(<KioskProcessingPage />, route);

    expect(await screen.findByRole('status')).toHaveTextContent('Sending your order');
    await waitFor(() => expect(screen.getByText('Done page')).toBeInTheDocument(), { timeout: 4000 });

    expect(kioskApi.placeOrder).toHaveBeenCalledTimes(1);
    expect(kioskApi.placeOrder).toHaveBeenCalledWith(
      expect.objectContaining({ orderType: 'Take Out', paymentPreference: 'discount', discountHint: 'senior', lines: [{ itemId: 'i1', itemVariantId: null, quantity: 2 }] }),
      expect.anything(),
    );
    expect(useKioskStore.getState().submitted?.kioskPrepNumber).toBe(42);
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(0);
  });

  it('keeps the cart and offers to try again when the connection drops, resending under the same order id', async () => {
    vi.mocked(kioskApi.placeOrder).mockRejectedValueOnce(new ApiError('network', 'down')).mockResolvedValueOnce(makeCart({ kioskPrepNumber: 5 }));
    renderPage(<KioskProcessingPage />, route);

    expect(await screen.findByRole('heading', { name: 'We could not send your order' })).toBeInTheDocument();
    expect(screen.getByText('Your order is safe. Please try again.')).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(1);

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await waitFor(() => expect(screen.getByText('Done page')).toBeInTheDocument(), { timeout: 4000 });

    const [first, second] = vi.mocked(kioskApi.placeOrder).mock.calls;
    expect(first?.[0].orderId).toBeTruthy();
    expect(second?.[0].orderId).toBe(first?.[0].orderId);
  });

  it('sends the customer to review their order when the order itself is refused, instead of retrying the same thing', async () => {
    vi.mocked(kioskApi.placeOrder).mockRejectedValue(new ApiError('validation', 'Iced Latte is no longer available.'));
    renderPage(<KioskProcessingPage />, route);

    expect(await screen.findByRole('alert')).toHaveTextContent('Iced Latte is no longer available.');
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Review my order' }));
    expect(await screen.findByText('Cart page')).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(1);
  });

  it('points to a member of staff after the third failed try', async () => {
    vi.mocked(kioskApi.placeOrder).mockRejectedValue(new ApiError('network', 'down'));
    renderPage(<KioskProcessingPage />, route);

    for (let attempt = 1; attempt <= 3; attempt++) {
      await waitFor(() => expect(kioskApi.placeOrder).toHaveBeenCalledTimes(attempt));
      const retry = await screen.findByRole('button', { name: 'Try again' });
      if (attempt < 3) {
        expect(screen.queryByText(/ask a member of staff/i)).toBeNull();
        fireEvent.click(retry);
      }
    }
    expect(await screen.findByText(/ask a member of staff/i)).toBeInTheDocument();
  });

  it('can go back to payment from a failure', async () => {
    vi.mocked(kioskApi.placeOrder).mockRejectedValue(new ApiError('serviceUnavailable', 'busy'));
    renderPage(<KioskProcessingPage />, route);
    fireEvent.click(await screen.findByRole('button', { name: 'Back to payment' }));
    expect(await screen.findByText('Payment page')).toBeInTheDocument();
  });

  it('does not send anything when there is no order to send', async () => {
    useLocalKioskCartStore.setState({ lines: [] });
    renderPage(<KioskProcessingPage />, route);
    expect(await screen.findByText('Menu page')).toBeInTheDocument();
    expect(kioskApi.placeOrder).not.toHaveBeenCalled();
  });
});
