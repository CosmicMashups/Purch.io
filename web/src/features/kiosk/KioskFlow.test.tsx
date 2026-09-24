import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { makeCart, makeItem, makeLine } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { catalogApi } from '../catalog/api';
import { kioskApi } from './api';
import { DONE_RESET_MS, KioskDonePage } from './KioskDonePage';
import { KioskCartPage } from './KioskCartPage';
import { KioskMenuPage } from './KioskMenuPage';
import { KioskOrderTypePage } from './KioskOrderTypePage';
import { useKioskStore } from './kioskStore';
import { kioskAddQueue } from './queries';

vi.mock('./api', () => ({
  deviceApi: { pair: vi.fn() },
  kioskApi: { getCart: vi.fn(), addLine: vi.fn(), updateLine: vi.fn(), removeLine: vi.fn(), setOrderType: vi.fn(), submit: vi.fn(), branding: vi.fn() },
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
  kioskAddQueue.reset();
  signInAs('Kiosk', { device_id: 'k1', branch_id: 'b1' });
  vi.mocked(catalogApi.listItems).mockResolvedValue([
    makeItem({ id: 'latte', name: 'Iced Latte', basePrice: 150 }),
    makeItem({ id: 'gone', name: 'Sold Out Cake', isOutOfStock: true }),
    makeItem({ id: 'fish', name: 'Fish By Weight', pricingType: 2 }),
  ]);
  vi.mocked(catalogApi.listCategories).mockResolvedValue([]);
  vi.mocked(catalogApi.listItemModifierGroups).mockResolvedValue([]);
  vi.mocked(kioskApi.getCart).mockResolvedValue(makeCart());
});
afterEach(() => vi.useRealTimers());

describe('KioskMenuPage', () => {
  it('adds a plain item to the kiosk cart, not the cashier cart', async () => {
    vi.mocked(kioskApi.addLine).mockResolvedValue(makeCart({ lines: [makeLine({ id: 'l1', itemName: 'Iced Latte' })], totalAmount: 150 }));
    renderPage(<KioskMenuPage />, { otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Iced Latte/ }));
    await waitFor(() => expect(kioskApi.addLine).toHaveBeenCalledWith({ itemId: 'latte', itemVariantId: null, quantity: 1 }));
    expect(await screen.findByText(/View your order \(1\)/)).toBeInTheDocument();
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
    expect(kioskApi.addLine).not.toHaveBeenCalled();
  });
});

describe('KioskMenuPage quick taps', () => {
  it('lets a customer keep tapping while the first item is still being added', async () => {
    let finish!: (c: ReturnType<typeof makeCart>) => void;
    vi.mocked(kioskApi.addLine)
      .mockReturnValueOnce(new Promise((r) => (finish = r)))
      .mockResolvedValue(makeCart());
    vi.mocked(catalogApi.listItems).mockResolvedValue([makeItem({ id: 'latte', name: 'Iced Latte', basePrice: 150 }), makeItem({ id: 'mocha', name: 'Mocha', basePrice: 170 })]);
    renderPage(<KioskMenuPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Iced Latte/ }));
    const mocha = screen.getByRole('button', { name: /Mocha/ });
    expect(mocha).toBeEnabled();
    fireEvent.click(mocha);

    expect(await screen.findByText(/View your order \(2\)/)).toBeInTheDocument();
    finish(makeCart({ lines: [makeLine({ id: 'a', itemName: 'Iced Latte' })] }));
    await waitFor(() => expect(kioskApi.addLine).toHaveBeenCalledTimes(2));
  });

  it('will not continue to the next step while an item is still being added', async () => {
    vi.mocked(kioskApi.getCart).mockResolvedValue(makeCart({ lines: [makeLine({ id: 'a', itemName: 'Iced Latte' })], totalAmount: 150 }));
    vi.mocked(kioskApi.addLine).mockReturnValue(new Promise(() => undefined));
    const { kioskAddQueue: q } = await import('./queries');
    renderPage(<KioskCartPage />, { otherRoutes: other });
    await screen.findAllByText('Iced Latte');
    q.add({ send: kioskApi.addLine, onCart: () => undefined, onError: () => undefined }, 'Mocha', { itemId: 'mocha', itemVariantId: null, quantity: 1 });
    expect(await screen.findByRole('status')).toHaveTextContent(/Adding Mocha/);
    expect(screen.getByRole('link', { name: 'Continue' })).toHaveAttribute('aria-disabled', 'true');
  });
});

describe('KioskCartPage', () => {
  it('shows the server total and changes a quantity through the kiosk endpoint', async () => {
    vi.mocked(kioskApi.getCart).mockResolvedValue(makeCart({ totalAmount: 300, lines: [makeLine({ id: 'l1', itemName: 'Iced Latte', quantity: 2, lineTotal: 300 })] }));
    vi.mocked(kioskApi.updateLine).mockResolvedValue(makeCart({ totalAmount: 450, lines: [makeLine({ id: 'l1', itemName: 'Iced Latte', quantity: 3, lineTotal: 450 })] }));
    renderPage(<KioskCartPage />, { otherRoutes: other });
    expect(await screen.findAllByText('₱300.00')).toHaveLength(2);
    fireEvent.click(screen.getByRole('button', { name: 'More Iced Latte' }));
    await waitFor(() => expect(kioskApi.updateLine).toHaveBeenCalledWith('l1', 3));
    expect(await screen.findAllByText('₱450.00')).not.toHaveLength(0);
  });

  it('removes a line', async () => {
    vi.mocked(kioskApi.getCart).mockResolvedValue(makeCart({ lines: [makeLine({ id: 'l1', itemName: 'Iced Latte' })] }));
    vi.mocked(kioskApi.removeLine).mockResolvedValue(makeCart());
    renderPage(<KioskCartPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Remove' }));
    await waitFor(() => expect(kioskApi.removeLine).toHaveBeenCalledWith('l1'));
    expect(await screen.findByText('Your order is empty')).toBeInTheDocument();
  });

  it('will not continue with an empty order', async () => {
    renderPage(<KioskCartPage />);
    expect(await screen.findByText('Your order is empty')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Continue' })).toBeNull();
  });
});

describe('KioskOrderTypePage', () => {
  it('sets the order type then submits, and shows the confirmation', async () => {
    vi.mocked(kioskApi.getCart).mockResolvedValue(makeCart({ lines: [makeLine({ id: 'l1', itemName: 'Iced Latte' })] }));
    vi.mocked(kioskApi.setOrderType).mockResolvedValue(makeCart());
    vi.mocked(kioskApi.submit).mockResolvedValue(makeCart({ kioskPrepNumber: 42, orderType: 'Take Out' }));
    renderPage(<KioskOrderTypePage />, { route: '/kiosk/order-type', path: '/kiosk/order-type', otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Take Out/ }));
    expect(await screen.findByText('Done page')).toBeInTheDocument();
    expect(kioskApi.setOrderType).toHaveBeenCalledWith('Take Out');
    expect(useKioskStore.getState().submitted?.kioskPrepNumber).toBe(42);
  });

  it('does not submit when setting the order type fails', async () => {
    vi.mocked(kioskApi.getCart).mockResolvedValue(makeCart({ lines: [makeLine({ id: 'l1', itemName: 'Iced Latte' })] }));
    vi.mocked(kioskApi.setOrderType).mockRejectedValue(new Error('boom'));
    renderPage(<KioskOrderTypePage />, { route: '/kiosk/order-type', path: '/kiosk/order-type', otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Dine In/ }));
    await waitFor(() => expect(useToastStore.getState().toasts).toHaveLength(1));
    expect(kioskApi.submit).not.toHaveBeenCalled();
    expect(screen.queryByText('Done page')).toBeNull();
  });

  it('goes back to the menu when the order is empty', async () => {
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
