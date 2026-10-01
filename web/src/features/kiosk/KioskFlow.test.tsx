import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { makeCart, makeItem } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { catalogApi } from '../catalog/api';
import { kioskApi } from './api';
import { DONE_RESET_MS, KioskDonePage } from './KioskDonePage';
import { KioskCartPage } from './KioskCartPage';
import { KioskMenuPage } from './KioskMenuPage';
import { KioskOrderTypePage } from './KioskOrderTypePage';
import { useKioskStore } from './kioskStore';
import { useLocalKioskCartStore } from './localCart';

vi.mock('./api', () => ({
  deviceApi: { pair: vi.fn() },
  kioskApi: { placeOrder: vi.fn(), branding: vi.fn() },
  displayApi: { pending: vi.fn(), setKitchenStatus: vi.fn() },
}));
vi.mock('../catalog/api', () => ({
  catalogApi: { listItems: vi.fn(), listModifierGroups: vi.fn(), listCategories: vi.fn(), listItemModifierGroups: vi.fn(), listVariants: vi.fn(), listComboComponents: vi.fn() },
}));

const other = [
  { path: '/kiosk', element: <p>Kiosk start</p> },
  { path: '/kiosk/cart', element: <p>Cart page</p> },
  { path: '/kiosk/menu', element: <p>Menu page</p> },
  { path: '/kiosk/order-type', element: <p>Order type page</p> },
  { path: '/kiosk/done', element: <p>Done page</p> },
];

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  useKioskStore.setState({ submitted: null });
  useLocalKioskCartStore.setState({ lines: [], lastActivityAt: Date.now() });
  signInAs('Kiosk', { device_id: 'k1', branch_id: 'b1' });
  vi.mocked(catalogApi.listItems).mockResolvedValue([
    makeItem({ id: 'latte', name: 'Iced Latte', basePrice: 150 }),
    makeItem({ id: 'gone', name: 'Sold Out Cake', isOutOfStock: true }),
    makeItem({ id: 'fish', name: 'Fish By Weight', pricingType: 2 }),
  ]);
  vi.mocked(catalogApi.listCategories).mockResolvedValue([]);
  vi.mocked(catalogApi.listModifierGroups).mockResolvedValue([]);
  vi.mocked(catalogApi.listItemModifierGroups).mockResolvedValue([]);
});
afterEach(() => vi.useRealTimers());

describe('KioskMenuPage', () => {
  it('adds a plain item to the kiosk cart entirely on-device, with no network call', async () => {
    renderPage(<KioskMenuPage />, { otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Iced Latte/ }));
    expect(await screen.findByText(/View your order \(1\)/)).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(1);
    expect(kioskApi.placeOrder).not.toHaveBeenCalled();
  });

  it('does not let a sold out item be ordered', async () => {
    renderPage(<KioskMenuPage />);
    expect(await screen.findByRole('button', { name: /Sold Out Cake/ })).toBeDisabled();
  });

  it('sends a weighed item to the counter instead of guessing a weight', async () => {
    const { PricingType } = await import('../catalog/types');
    vi.mocked(catalogApi.listItems).mockResolvedValue([makeItem({ id: 'fish', name: 'Fish By Weight', pricingType: PricingType.WeightVolume })]);
    renderPage(<KioskMenuPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Fish By Weight/ }));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toMatch(/order at the counter/i));
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(0);
  });

  it('merges a second tap of the same plain item into one line, like the server would', async () => {
    renderPage(<KioskMenuPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Iced Latte/ }));
    fireEvent.click(await screen.findByRole('button', { name: /Iced Latte/ }));
    await waitFor(() => expect(useLocalKioskCartStore.getState().lines).toHaveLength(1));
    expect(useLocalKioskCartStore.getState().lines[0]?.quantity).toBe(2);
  });

  it('lets a customer keep tapping instantly, one after another', async () => {
    vi.mocked(catalogApi.listItems).mockResolvedValue([makeItem({ id: 'latte', name: 'Iced Latte', basePrice: 150 }), makeItem({ id: 'mocha', name: 'Mocha', basePrice: 170 })]);
    renderPage(<KioskMenuPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Iced Latte/ }));
    fireEvent.click(screen.getByRole('button', { name: /Mocha/ }));
    expect(await screen.findByText(/View your order \(2\)/)).toBeInTheDocument();
  });
});

describe('KioskCartPage', () => {
  it('shows the on-device total and changes a quantity locally', async () => {
    useLocalKioskCartStore.setState({ lines: [{ localId: 'l1', itemId: 'i1', itemName: 'Iced Latte', itemVariantId: null, itemVariantAttributes: {}, quantity: 2, unitPrice: 150, comboSelections: [], modifierSelections: [] }], lastActivityAt: Date.now() });
    renderPage(<KioskCartPage />, { otherRoutes: other });
    expect(await screen.findAllByText('₱300.00')).toHaveLength(2);
    fireEvent.click(screen.getByRole('button', { name: 'More Iced Latte' }));
    expect(await screen.findAllByText('₱450.00')).not.toHaveLength(0);
    expect(kioskApi.placeOrder).not.toHaveBeenCalled();
  });

  it('removes a line', async () => {
    useLocalKioskCartStore.setState({ lines: [{ localId: 'l1', itemId: 'i1', itemName: 'Iced Latte', itemVariantId: null, itemVariantAttributes: {}, quantity: 1, unitPrice: 150, comboSelections: [], modifierSelections: [] }], lastActivityAt: Date.now() });
    renderPage(<KioskCartPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Remove' }));
    expect(await screen.findByText('Your order is empty')).toBeInTheDocument();
  });

  it('will not continue with an empty order', async () => {
    renderPage(<KioskCartPage />);
    expect(await screen.findByText('Your order is empty')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Continue' })).toBeNull();
  });
});

describe('KioskOrderTypePage', () => {
  beforeEach(() => {
    useLocalKioskCartStore.setState({ lines: [{ localId: 'l1', itemId: 'i1', itemName: 'Iced Latte', itemVariantId: null, itemVariantAttributes: {}, quantity: 1, unitPrice: 150, comboSelections: [], modifierSelections: [] }], lastActivityAt: Date.now() });
  });

  it('places the whole order in one call and shows the confirmation', async () => {
    vi.mocked(kioskApi.placeOrder).mockResolvedValue(makeCart({ kioskPrepNumber: 42, orderType: 'Take Out' }));
    renderPage(<KioskOrderTypePage />, { route: '/kiosk/order-type', path: '/kiosk/order-type', otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Take Out/ }));
    expect(await screen.findByText('Done page')).toBeInTheDocument();
    expect(kioskApi.placeOrder).toHaveBeenCalledTimes(1);
    expect(kioskApi.placeOrder).toHaveBeenCalledWith(expect.objectContaining({ orderType: 'Take Out', lines: [{ itemId: 'i1', itemVariantId: null, quantity: 1 }] }), expect.anything());
    expect(useKioskStore.getState().submitted?.kioskPrepNumber).toBe(42);
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(0);
  });

  it('shows an error and keeps the cart when the order is refused', async () => {
    vi.mocked(kioskApi.placeOrder).mockRejectedValue(new Error('boom'));
    renderPage(<KioskOrderTypePage />, { route: '/kiosk/order-type', path: '/kiosk/order-type', otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Dine In/ }));
    await waitFor(() => expect(useToastStore.getState().toasts).toHaveLength(1));
    expect(screen.queryByText('Done page')).toBeNull();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(1);
  });

  it('goes back to the menu when the order is empty', async () => {
    useLocalKioskCartStore.setState({ lines: [], lastActivityAt: Date.now() });
    renderPage(<KioskOrderTypePage />, { route: '/kiosk/order-type', path: '/kiosk/order-type', otherRoutes: other });
    expect(await screen.findByText('Menu page')).toBeInTheDocument();
  });
});

describe('KioskDonePage', () => {
  it('shows the order number and lets the next customer start', () => {
    useKioskStore.setState({ submitted: makeCart({ kioskPrepNumber: 7, orderType: 'Dine In' }) });
    renderPage(<KioskDonePage />, { route: '/kiosk/done', path: '/kiosk/done', otherRoutes: other });
    expect(screen.getByLabelText('Order number 7')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Start a new order' }));
    expect(screen.getByText('Kiosk start')).toBeInTheDocument();
    expect(useKioskStore.getState().submitted).toBeNull();
  });

  it('resets by itself for the next customer', () => {
    vi.useFakeTimers();
    useKioskStore.setState({ submitted: makeCart({ kioskPrepNumber: 7 }) });
    renderPage(<KioskDonePage />, { route: '/kiosk/done', path: '/kiosk/done', otherRoutes: other });
    expect(screen.getByLabelText('Order number 7')).toBeInTheDocument();
    vi.advanceTimersByTime(DONE_RESET_MS + 10);
    expect(useKioskStore.getState().submitted).toBeNull();
  });

  it('returns to the start after a reload, when nothing was kept', () => {
    renderPage(<KioskDonePage />, { route: '/kiosk/done', path: '/kiosk/done', otherRoutes: other });
    expect(screen.getByText('Kiosk start')).toBeInTheDocument();
  });
});
