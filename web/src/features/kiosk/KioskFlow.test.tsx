import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { makeCart, makeItem } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { catalogApi } from '../catalog/api';
import { PricingType } from '../catalog/types';
import { tenantApi } from '../tenant/api';
import { kioskApi } from './api';
import { DONE_RESET_MS, KioskDonePage } from './KioskDonePage';
import { KioskCartPage } from './KioskCartPage';
import { KioskItemPage } from './KioskItemPage';
import { KioskLandingPage } from './KioskLandingPage';
import { KioskMenuPage } from './KioskMenuPage';
import { KioskOrderTypePage } from './KioskOrderTypePage';
import { KioskPaymentPage } from './KioskPaymentPage';
import { useKioskStore } from './kioskStore';
import { useLocalKioskCartStore, type LocalCartLine } from './localCart';

vi.mock('./api', () => ({
  deviceApi: { pair: vi.fn() },
  kioskApi: { placeOrder: vi.fn(), branding: vi.fn(), promoRules: vi.fn() },
  displayApi: { pending: vi.fn(), setKitchenStatus: vi.fn() },
}));
vi.mock('../catalog/api', () => ({
  catalogApi: { listItems: vi.fn(), listModifierGroups: vi.fn(), listCategories: vi.fn(), listItemModifierGroups: vi.fn(), listVariants: vi.fn(), listComboComponents: vi.fn() },
}));
vi.mock('../tenant/api', () => ({ tenantApi: { get: vi.fn() } }));
// The real overlay is a GSAP timeline; here it is a button that finishes it, so the test decides when it is "done".
vi.mock('./AddedToOrderOverlay', () => ({
  AddedToOrderOverlay: ({ title, subtitle, onDone }: { title: string; subtitle?: string; onDone: () => void }) => (
    <div role="status">
      <p>{title}</p>
      <p>{subtitle}</p>
      <button type="button" onClick={onDone}>
        finish animation
      </button>
    </div>
  ),
}));

const other = [
  { path: '/kiosk', element: <p>Kiosk start</p> },
  { path: '/kiosk/cart', element: <p>Cart page</p> },
  { path: '/kiosk/menu', element: <p>Menu page</p> },
  { path: '/kiosk/item/:itemId', element: <p>Item page</p> },
  { path: '/kiosk/order-type', element: <p>Order type page</p> },
  { path: '/kiosk/payment', element: <p>Payment page</p> },
  { path: '/kiosk/processing', element: <p>Processing page</p> },
  { path: '/kiosk/done', element: <p>Done page</p> },
];

const latteLine: LocalCartLine = { localId: 'l1', itemId: 'latte', itemName: 'Iced Latte', itemVariantId: null, itemVariantAttributes: {}, quantity: 1, unitPrice: 150, comboSelections: [], modifierSelections: [] };

const meal = makeItem({ id: 'meal', name: 'Chicken Meal', basePrice: 99, categoryId: 'meals' });
const sides = { id: 'g-sides', name: 'Add fries & sides', allowMultipleSelection: true, isRequired: false, modifiers: [{ id: 'fries', name: 'Fries', priceDelta: 40 }, { id: 'rings', name: 'Onion rings', priceDelta: 45 }] };
const drinks = {
  id: 'g-drinks',
  name: 'Add drinks',
  allowMultipleSelection: false,
  isRequired: false,
  modifiers: [{ id: 'coke', name: 'Coke Zero', priceDelta: 25 }, { id: 'sprite', name: 'Sprite', priceDelta: 25, isOutOfStock: true }],
};

beforeEach(() => {
  vi.clearAllMocks();
  window.sessionStorage.clear();
  useToastStore.setState({ toasts: [] });
  useKioskStore.setState({ submitted: null });
  useKioskStore.getState().resetCheckout();
  useLocalKioskCartStore.setState({ lines: [], lastActivityAt: Date.now() });
  signInAs('Kiosk', { device_id: 'k1', branch_id: 'b1' });
  vi.mocked(tenantApi.get).mockResolvedValue({ name: 'Mang Inasal', useSeparateInventoryTracking: false } as Awaited<ReturnType<typeof tenantApi.get>>);
  vi.mocked(catalogApi.listItems).mockResolvedValue([
    makeItem({ id: 'latte', name: 'Iced Latte', basePrice: 150, categoryId: 'coffee' }),
    makeItem({ id: 'gone', name: 'Sold Out Cake', isOutOfStock: true, categoryId: 'bakery' }),
    makeItem({ id: 'fish', name: 'Fish By Weight', pricingType: PricingType.WeightVolume, categoryId: 'bakery' }),
    meal,
  ]);
  vi.mocked(catalogApi.listCategories).mockResolvedValue([
    { id: 'coffee', name: 'Coffee', sortOrder: 1, imageUrl: null },
    { id: 'bakery', name: 'Bakery', sortOrder: 2, imageUrl: null },
    { id: 'meals', name: 'Meals', sortOrder: 3, imageUrl: null },
  ]);
  vi.mocked(catalogApi.listModifierGroups).mockResolvedValue([]);
  vi.mocked(catalogApi.listItemModifierGroups).mockResolvedValue([]);
  vi.mocked(catalogApi.listVariants).mockResolvedValue([]);
  vi.mocked(catalogApi.listComboComponents).mockResolvedValue([]);
  vi.mocked(kioskApi.promoRules).mockRejectedValue(new Error('no rules'));
});
afterEach(() => vi.useRealTimers());

describe('KioskLandingPage', () => {
  it('is one big tap target with the only words along the bottom, and no badge or full screen button', () => {
    renderPage(<KioskLandingPage />, { otherRoutes: other });
    expect(screen.getByRole('link', { name: /Tap anywhere to begin/ })).toHaveAttribute('href', '/kiosk/menu');
    expect(screen.queryByText(/Self-service order kiosk/i)).toBeNull();
    expect(screen.queryByRole('button', { name: /full ?screen/i })).toBeNull();
    expect(screen.queryByText(/Start your order/i)).toBeNull();
  });
});

describe('KioskMenuPage', () => {
  it('shows every category in one scroll, each under its heading, with the rail on the left and no "All"', async () => {
    renderPage(<KioskMenuPage />, { otherRoutes: other });
    await screen.findByRole('button', { name: /Iced Latte/ });

    expect(screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent)).toEqual(['Coffee', 'Bakery', 'Meals']);
    const rail = screen.getByRole('navigation', { name: 'Categories' });
    expect(within(rail).getAllByRole('button').map((tile) => tile.textContent)).toEqual(['Coffee', 'Bakery', 'Meals']);
    expect(within(rail).queryByRole('button', { name: 'All' })).toBeNull();
  });

  it('opens the item page when an item is tapped, adding nothing to the order yet', async () => {
    renderPage(<KioskMenuPage />, { otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Iced Latte/ }));
    expect(await screen.findByText('Item page')).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(0);
    expect(kioskApi.placeOrder).not.toHaveBeenCalled();
  });

  it('does not let a sold out item be ordered', async () => {
    renderPage(<KioskMenuPage />);
    expect(await screen.findByRole('button', { name: /Sold Out Cake/ })).toBeDisabled();
  });

  it('sends a weighed item to the counter instead of guessing a weight', async () => {
    renderPage(<KioskMenuPage />, { otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Fish By Weight/ }));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toMatch(/order it at the counter/i));
    expect(screen.queryByText('Item page')).toBeNull();
  });

  it('keeps Complete Order out of reach until something is in the order, and shows the total apart from the button', async () => {
    renderPage(<KioskMenuPage />, { otherRoutes: other });
    expect(await screen.findByRole('button', { name: 'Complete Order' })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Start Over/ })).toBeInTheDocument();

    useLocalKioskCartStore.setState({ lines: [{ ...latteLine, quantity: 2 }] });
    const complete = await screen.findByRole('link', { name: 'Complete Order' });
    expect(complete).toHaveAttribute('href', '/kiosk/cart');
    expect(complete).not.toHaveTextContent('₱');
    expect(screen.getByText('₱300.00')).toBeInTheDocument();
  });

  it('asks before clearing an order that has items, and clears it on confirmation', async () => {
    useLocalKioskCartStore.setState({ lines: [latteLine] });
    renderPage(<KioskMenuPage />, { otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Start Over/ }));
    expect(screen.getByRole('dialog', { name: 'Start over?' })).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(1);

    fireEvent.click(screen.getByRole('button', { name: 'Clear and start over' }));
    expect(await screen.findByText('Kiosk start')).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(0);
  });
});

describe('KioskItemPage', () => {
  const route = { route: '/kiosk/item/latte', path: '/kiosk/item/:itemId', otherRoutes: other };

  it('opens for a plain item with just quantity, and adds it to the order with a confirmation before going back to the menu', async () => {
    renderPage(<KioskItemPage />, route);
    expect(await screen.findByRole('heading', { name: 'Iced Latte' })).toBeInTheDocument();
    expect(screen.queryByRole('tablist')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Increase quantity' }));
    expect(screen.getByText('₱300.00')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Add to order' }));

    expect(await screen.findByText('Added to your order')).toBeInTheDocument();
    expect(screen.getByText('2 x Iced Latte')).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toMatchObject([{ itemId: 'latte', quantity: 2, unitPrice: 150 }]);
    expect(screen.queryByText('Menu page')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'finish animation' }));
    expect(await screen.findByText('Menu page')).toBeInTheDocument();
  });

  it('keeps Add to order disabled until the item options have loaded, so an early tap cannot do nothing', async () => {
    let release: (groups: never[]) => void = () => undefined;
    vi.mocked(catalogApi.listItemModifierGroups).mockReturnValue(new Promise((resolve) => (release = resolve)));
    renderPage(<KioskItemPage />, route);
    expect(await screen.findByRole('button', { name: 'Add to order' })).toBeDisabled();

    release([]);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Add to order' })).toBeEnabled());
  });

  it('cancels back to the menu without adding anything', async () => {
    renderPage(<KioskItemPage />, route);
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel' }));
    expect(await screen.findByText('Menu page')).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(0);
  });

  describe('with modifier groups', () => {
    const mealRoute = { route: '/kiosk/item/meal', path: '/kiosk/item/:itemId', otherRoutes: other };
    beforeEach(() => vi.mocked(catalogApi.listItemModifierGroups).mockResolvedValue([sides, drinks]));

    it('puts each group in its own tab and will not add until every group has an explicit choice', async () => {
      renderPage(<KioskItemPage />, mealRoute);
      const tabs = await screen.findAllByRole('tab');
      expect(tabs.map((tab) => tab.querySelector('span span')?.textContent)).toEqual(['Quantity', 'Add fries & sides', 'Add drinks']);
      expect(screen.getByRole('button', { name: 'Add to order' })).toBeDisabled();
      expect(screen.getByRole('status')).toHaveTextContent('Choose Add fries & sides');

      fireEvent.click(screen.getByRole('tab', { name: /Add fries & sides/ }));
      fireEvent.click(screen.getByRole('radio', { name: /No fries & sides/ }));
      expect(screen.getByRole('button', { name: 'Add to order' })).toBeDisabled();
      expect(screen.getByRole('status')).toHaveTextContent('Choose Add drinks');

      fireEvent.click(screen.getByRole('tab', { name: /Add drinks/ }));
      fireEvent.click(screen.getByRole('radio', { name: /No drinks/ }));
      expect(screen.getByRole('button', { name: 'Add to order' })).toBeEnabled();
    });

    it('prices the chosen sides and drink live and sends exactly those modifiers', async () => {
      renderPage(<KioskItemPage />, mealRoute);
      fireEvent.click(await screen.findByRole('tab', { name: /Add fries & sides/ }));
      fireEvent.click(screen.getByRole('checkbox', { name: /Fries/ }));
      fireEvent.click(screen.getByRole('checkbox', { name: /Onion rings/ }));
      fireEvent.click(screen.getByRole('tab', { name: /Add drinks/ }));
      fireEvent.click(screen.getByRole('radio', { name: /Coke Zero/ }));

      expect(screen.getByText('₱209.00')).toBeInTheDocument(); // 99 + 40 + 45 + 25
      fireEvent.click(screen.getByRole('button', { name: 'Add to order' }));

      expect(await screen.findByText('Added to your order')).toBeInTheDocument();
      const [line] = useLocalKioskCartStore.getState().lines;
      expect(line?.unitPrice).toBe(209);
      expect(line?.modifierSelections.map((selection) => selection.itemModifierId).sort()).toEqual(['coke', 'fries', 'rings']);
    });

    it('shows a sold-out modifier but does not let it be picked', async () => {
      renderPage(<KioskItemPage />, mealRoute);
      fireEvent.click(await screen.findByRole('tab', { name: /Add drinks/ }));
      const sprite = screen.getByRole('radio', { name: /Sprite/ });
      expect(sprite).toBeDisabled();
      expect(sprite).toHaveTextContent('Sold out');
    });

    it('offers Next to the tab that still needs a choice', async () => {
      renderPage(<KioskItemPage />, mealRoute);
      fireEvent.click(await screen.findByRole('button', { name: 'Next: Add fries & sides' }));
      expect(screen.getByRole('tab', { name: /Add fries & sides/ })).toHaveAttribute('aria-selected', 'true');
    });
  });

  it('opens an order line for editing with its choices filled in and replaces it in place', async () => {
    vi.mocked(catalogApi.listItemModifierGroups).mockResolvedValue([sides, drinks]);
    useLocalKioskCartStore.setState({
      lines: [
        { ...latteLine, localId: 'meal-line', itemId: 'meal', itemName: 'Chicken Meal', quantity: 1, unitPrice: 124, modifierSelections: [{ itemModifierId: 'coke', modifierName: 'Coke Zero', modifierGroupName: 'Add drinks', priceDelta: 25 }] },
        { ...latteLine, localId: 'after', itemName: 'Iced Latte' },
      ],
    });
    renderPage(<KioskItemPage />, { route: '/kiosk/item/meal?line=meal-line', path: '/kiosk/item/:itemId', otherRoutes: other });

    // The drink they chose is still chosen, and the sides were deliberately skipped, so nothing is missing.
    fireEvent.click(await screen.findByRole('tab', { name: /Add drinks/ }));
    await waitFor(() => expect(screen.getByRole('radio', { name: /Coke Zero/ })).toBeChecked());
    await waitFor(() => expect(screen.getByRole('button', { name: 'Update order' })).toBeEnabled());

    fireEvent.click(screen.getByRole('radio', { name: /No drinks/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Update order' }));
    expect(await screen.findByText('Order updated')).toBeInTheDocument();

    const lines = useLocalKioskCartStore.getState().lines;
    expect(lines.map((line) => line.localId)).toEqual(['meal-line', 'after']);
    expect(lines[0]?.modifierSelections).toHaveLength(0);
    expect(lines[0]?.unitPrice).toBe(99);

    fireEvent.click(screen.getByRole('button', { name: 'finish animation' }));
    expect(await screen.findByText('Cart page')).toBeInTheDocument();
  });

  it('caps the quantity at what is left when the business counts stock on its items', async () => {
    vi.mocked(tenantApi.get).mockResolvedValue({ name: 'Mang Inasal', useSeparateInventoryTracking: false } as Awaited<ReturnType<typeof tenantApi.get>>);
    vi.mocked(catalogApi.listItems).mockResolvedValue([makeItem({ id: 'latte', name: 'Iced Latte', basePrice: 150, stockOnHand: 2 })]);
    renderPage(<KioskItemPage />, route);
    const plus = await screen.findByRole('button', { name: 'Increase quantity' });
    await waitFor(() => {
      fireEvent.click(plus);
      expect(plus).toBeDisabled();
    });
    expect(screen.getByText('Only 2 left')).toBeInTheDocument();
  });

  it('does not strand a customer on an item that is no longer on the menu', async () => {
    renderPage(<KioskItemPage />, { route: '/kiosk/item/missing', path: '/kiosk/item/:itemId', otherRoutes: other });
    expect(await screen.findByText('This item is no longer on the menu')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Back to menu' }));
    expect(await screen.findByText('Menu page')).toBeInTheDocument();
  });
});

describe('KioskCartPage', () => {
  it('shows each line with its picture slot, the on-device total and a local quantity change', async () => {
    useLocalKioskCartStore.setState({ lines: [{ ...latteLine, quantity: 2 }] });
    renderPage(<KioskCartPage />, { otherRoutes: other });
    expect(await screen.findByRole('heading', { name: 'Your order' })).toBeInTheDocument();
    expect((await screen.findAllByText('₱300.00')).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: 'More Iced Latte' }));
    expect((await screen.findAllByText('₱450.00')).length).toBeGreaterThan(0);
    expect(kioskApi.placeOrder).not.toHaveBeenCalled();
  });

  it('has Back to menu as a button beside Proceed to payment at the bottom, not a link at the top', async () => {
    useLocalKioskCartStore.setState({ lines: [latteLine] });
    renderPage(<KioskCartPage />, { otherRoutes: other });
    expect(await screen.findByRole('link', { name: 'Back to menu' })).toHaveAttribute('href', '/kiosk/menu');
    expect(screen.getByRole('link', { name: 'Proceed to payment' })).toHaveAttribute('href', '/kiosk/order-type');
    expect(screen.queryByRole('link', { name: 'Continue' })).toBeNull();
  });

  it('sends a line to its item page to be edited, and brings the customer back here afterwards', async () => {
    useLocalKioskCartStore.setState({ lines: [latteLine] });
    renderPage(<KioskCartPage />, { otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Edit/ }));
    expect(await screen.findByText('Item page')).toBeInTheDocument();
  });

  it('removes a line', async () => {
    useLocalKioskCartStore.setState({ lines: [latteLine] });
    renderPage(<KioskCartPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Remove/ }));
    expect(await screen.findByText('Your order is empty')).toBeInTheDocument();
  });

  it('will not proceed with an empty order', async () => {
    renderPage(<KioskCartPage />);
    expect(await screen.findByText('Your order is empty')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Proceed to payment' })).toBeNull();
  });

  it('holds back payment while a line has become unavailable, and says which', async () => {
    useLocalKioskCartStore.setState({ lines: [{ ...latteLine, localId: 'cake', itemId: 'gone', itemName: 'Sold Out Cake' }] });
    renderPage(<KioskCartPage />, { otherRoutes: other });
    expect(await screen.findByText('No longer available')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('no longer available');
    expect(screen.getByRole('button', { name: 'Proceed to payment' })).toBeDisabled();
  });
});

describe('KioskOrderTypePage', () => {
  beforeEach(() => useLocalKioskCartStore.setState({ lines: [latteLine] }));

  it('records the choice and moves on to payment without sending the order', async () => {
    renderPage(<KioskOrderTypePage />, { route: '/kiosk/order-type', path: '/kiosk/order-type', otherRoutes: other });
    fireEvent.click(await screen.findByRole('button', { name: /Take Out/ }));
    expect(await screen.findByText('Payment page')).toBeInTheDocument();
    expect(useKioskStore.getState().checkout.orderType).toBe('Take Out');
    expect(kioskApi.placeOrder).not.toHaveBeenCalled();
  });

  it('goes back to the menu when the order is empty', async () => {
    useLocalKioskCartStore.setState({ lines: [] });
    renderPage(<KioskOrderTypePage />, { route: '/kiosk/order-type', path: '/kiosk/order-type', otherRoutes: other });
    expect(await screen.findByText('Menu page')).toBeInTheDocument();
  });
});

describe('KioskPaymentPage', () => {
  const route = { route: '/kiosk/payment', path: '/kiosk/payment', otherRoutes: other };
  beforeEach(() => {
    useLocalKioskCartStore.setState({ lines: [latteLine] });
    useKioskStore.getState().setOrderType('Dine In');
  });

  it('offers the four ways to pay, each with a name and a line saying what it means', async () => {
    renderPage(<KioskPaymentPage />, route);
    for (const name of ['Cash', 'Credit / Debit Card', 'E-Wallet (GCash / Maya)', 'With Discounts (Senior, PWD, Others)']) {
      expect(await screen.findByRole('button', { name: new RegExp(`^${name.replace(/[()/]/g, '\\$&')}`) })).toBeInTheDocument();
    }
    expect(screen.getByRole('button', { name: 'Place order' })).toBeDisabled();
  });

  it('records the choice and moves on to sending the order', async () => {
    renderPage(<KioskPaymentPage />, route);
    fireEvent.click(await screen.findByRole('button', { name: /E-Wallet/ }));
    expect(useKioskStore.getState().checkout.payment).toBe('ewallet');
    fireEvent.click(screen.getByRole('button', { name: 'Place order' }));
    expect(await screen.findByText('Processing page')).toBeInTheDocument();
  });

  it('asks which discount, keeps the total as an estimate, and does not change the price', async () => {
    renderPage(<KioskPaymentPage />, route);
    fireEvent.click(await screen.findByRole('button', { name: /With Discounts/ }));
    const sheet = screen.getByRole('dialog', { name: 'Which discount?' });
    expect(within(sheet).getByText(/valid ID ready/)).toBeInTheDocument();

    fireEvent.click(within(sheet).getByRole('radio', { name: 'Senior Citizen' }));
    expect(useKioskStore.getState().checkout).toMatchObject({ payment: 'discount', discountHint: 'senior' });
    expect(screen.getByText('Estimated total (before discount)')).toBeInTheDocument();
    expect(screen.getByText('₱150.00')).toBeInTheDocument();
  });

  it('sends the customer back a step when the order type was never chosen', async () => {
    useKioskStore.getState().resetCheckout();
    renderPage(<KioskPaymentPage />, route);
    expect(await screen.findByText('Order type page')).toBeInTheDocument();
  });
});

describe('KioskDonePage', () => {
  it('shows the order number, how they will pay and where to pay, and lets the next customer start', () => {
    useKioskStore.setState({ submitted: makeCart({ kioskPrepNumber: 7, orderType: 'Dine In', kioskPaymentPreference: 'discount', kioskDiscountHint: 'pwd' }) });
    renderPage(<KioskDonePage />, { route: '/kiosk/done', path: '/kiosk/done', otherRoutes: other });
    expect(screen.getByLabelText('Order number 7')).toBeInTheDocument();
    expect(screen.getByText('Pay at the counter and enjoy!')).toBeInTheDocument();
    expect(screen.getByText('With Discounts: PWD')).toBeInTheDocument();
    expect(screen.getByText('Dine In')).toBeInTheDocument();

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
