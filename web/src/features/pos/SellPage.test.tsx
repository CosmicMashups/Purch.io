import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { makeCart, makeItem, makeLine } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { catalogApi } from '../catalog/api';
import { PricingType } from '../catalog/types';
import { posApi } from './api';
import { SellPage } from './SellPage';

vi.mock('./api', () => ({
  posApi: {
    getCart: vi.fn(),
    addLine: vi.fn(),
    updateLine: vi.fn(),
    removeLine: vi.fn(),
    voidCart: vi.fn(),
    applyPromoCode: vi.fn(),
    applySeniorPwd: vi.fn(),
    setOrderType: vi.fn(),
    pay: vi.fn(),
  },
}));
vi.mock('../catalog/api', () => ({
  catalogApi: {
    listItems: vi.fn(),
    listCategories: vi.fn(),
    listItemModifierGroups: vi.fn(),
    listVariants: vi.fn(),
    listComboComponents: vi.fn(),
  },
}));

const items = [
  makeItem({ id: 'latte', name: 'Iced Latte', basePrice: 150, categoryId: 'coffee', barcode: '4800001' }),
  makeItem({ id: 'cookie', name: 'Ube Cookie', basePrice: 60, categoryId: 'bakery', isOutOfStock: true }),
  makeItem({ id: 'old', name: 'Retired Item', isActive: false }),
  makeItem({ id: 'tee', name: 'Logo Tee', pricingType: PricingType.VariantMatrix, categoryId: 'merch' }),
  makeItem({ id: 'mocha', name: 'Mocha', basePrice: 170, categoryId: 'coffee' }),
];

const driveSession = { device_id: 'dev1', branch_id: 'kat', scope_type: 'Branch', scope_id: 'kat' };

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  signInAs('Cashier', driveSession);
  vi.mocked(catalogApi.listItems).mockResolvedValue(items);
  vi.mocked(catalogApi.listCategories).mockResolvedValue([
    { id: 'coffee', name: 'Coffee', sortOrder: 1, imageUrl: null },
    { id: 'bakery', name: 'Bakery', sortOrder: 2, imageUrl: null },
  ]);
  vi.mocked(catalogApi.listItemModifierGroups).mockResolvedValue([]);
  vi.mocked(catalogApi.listVariants).mockResolvedValue([]);
  vi.mocked(catalogApi.listComboComponents).mockResolvedValue([]);
  vi.mocked(posApi.getCart).mockResolvedValue(makeCart());
});

describe('SellPage access', () => {
  it('asks an email sign-in (no device) to sign in with a device instead of selling', () => {
    signInAs('Admin');
    renderPage(<SellPage />);
    expect(screen.getByText('Sign in with a device to sell')).toBeInTheDocument();
    expect(posApi.getCart).not.toHaveBeenCalled();
  });
});

describe('SellPage catalog', () => {
  it('shows active items only, with a stock badge from the server flag', async () => {
    renderPage(<SellPage />);
    expect(await screen.findByRole('button', { name: /Iced Latte/ })).toBeInTheDocument();
    expect(screen.queryByText('Retired Item')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Ube Cookie/ })).toHaveTextContent('Out of stock');
  });

  it('filters by category and by search text', async () => {
    renderPage(<SellPage />);
    await screen.findByRole('button', { name: /Iced Latte/ });
    fireEvent.click(screen.getByRole('button', { name: 'Bakery' }));
    expect(screen.queryByRole('button', { name: /Iced Latte/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Ube Cookie/ })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'All' }));
    fireEvent.change(screen.getByLabelText('Search items or scan a barcode'), { target: { value: 'moc' } });
    expect(screen.getAllByRole('button', { name: /Mocha|Iced|Ube|Logo/ })).toHaveLength(1);
  });
});

describe('adding to the cart', () => {
  it('adds a plain item at quantity 1 and shows the server-priced cart', async () => {
    vi.mocked(posApi.addLine).mockResolvedValue(
      makeCart({ lines: [makeLine({ id: 'l1', itemName: 'Iced Latte', unitPrice: 150, lineTotal: 150 })], subtotal: 150, totalAmount: 150 }),
    );
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Iced Latte/ }));

    await waitFor(() => expect(posApi.addLine).toHaveBeenCalledWith({ itemId: 'latte', itemVariantId: null, quantity: 1 }));
    const cart = await screen.findByRole('region', { name: 'Cart' });
    expect(within(cart).getByText('Iced Latte')).toBeInTheDocument();
    expect(within(cart).getByTestId('cart-total')).toHaveTextContent('₱150.00');
  });

  it('shows the server total even when it differs from the lines, because the web never prices', async () => {
    vi.mocked(posApi.getCart).mockResolvedValue(
      makeCart({
        lines: [makeLine({ id: 'a', itemName: 'Iced Latte', lineTotal: 150 }), makeLine({ id: 'b', itemName: 'Mocha', lineTotal: 150 })],
        subtotal: 300,
        itemPromoDiscountAmount: 50,
        totalAmount: 250,
      }),
    );
    renderPage(<SellPage />);
    const cart = await screen.findByRole('region', { name: 'Cart' });
    expect(within(cart).getByTestId('cart-total')).toHaveTextContent('₱250.00');
    expect(within(cart).getByText('Item promotions')).toBeInTheDocument();
    expect(within(cart).getByRole('button', { name: /Charge ₱250\.00/ })).toBeEnabled();
  });

  it('opens the options dialog for an item with a required modifier group and blocks until it is chosen', async () => {
    vi.mocked(catalogApi.listItemModifierGroups).mockResolvedValue([
      {
        id: 'sugar',
        name: 'Sugar level',
        allowMultipleSelection: false,
        isRequired: true,
        modifiers: [
          { id: 'less', name: 'Less sugar', priceDelta: 0 },
          { id: 'extra', name: 'Extra shot', priceDelta: 30 },
        ],
      },
    ]);
    vi.mocked(posApi.addLine).mockResolvedValue(makeCart());
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Iced Latte/ }));

    const dialog = await screen.findByRole('dialog', { name: 'Iced Latte' });
    const add = within(dialog).getByRole('button', { name: 'Add to cart' });
    expect(add).toBeDisabled();
    expect(within(dialog).getByText('Choose Sugar level')).toBeInTheDocument();

    fireEvent.click(within(dialog).getByLabelText(/Extra shot/));
    expect(add).toBeEnabled();
    fireEvent.click(add);
    await waitFor(() => expect(posApi.addLine).toHaveBeenCalledWith({ itemId: 'latte', itemVariantId: null, quantity: 1, selectedModifierIds: ['extra'] }));
    expect(posApi.addLine).toHaveBeenCalledTimes(1);
  });

  it('requires a variant for a variant item', async () => {
    vi.mocked(catalogApi.listVariants).mockResolvedValue([
      { id: 'v-m', itemId: 'tee', attributes: { Size: 'M' }, sku: null, priceOverride: 450, imageUrl: null },
      { id: 'v-l', itemId: 'tee', attributes: { Size: 'L' }, sku: null, priceOverride: null, imageUrl: null },
    ]);
    vi.mocked(posApi.addLine).mockResolvedValue(makeCart());
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Logo Tee/ }));

    const dialog = await screen.findByRole('dialog', { name: 'Logo Tee' });
    expect(await within(dialog).findByText('Choose a variant')).toBeInTheDocument();
    fireEvent.click(within(dialog).getByLabelText(/^M/));
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add to cart' }));
    await waitFor(() => expect(posApi.addLine).toHaveBeenCalledWith({ itemId: 'tee', itemVariantId: 'v-m', quantity: 1 }));
  });

  it('collects one choice per combo slot unit and sends them', async () => {
    vi.mocked(catalogApi.listItems).mockResolvedValue([
      makeItem({ id: 'meal', name: 'Meal Deal', pricingType: PricingType.Combo, categoryId: 'combos' }),
      makeItem({ id: 'latte', name: 'Iced Latte', categoryId: 'drinks' }),
      makeItem({ id: 'tea', name: 'Iced Tea', categoryId: 'drinks' }),
    ]);
    vi.mocked(catalogApi.listComboComponents).mockResolvedValue([
      { id: 'slot1', itemId: 'meal', componentCategoryId: 'drinks', slotLabel: 'Drink', quantity: 2, substitutionUpchargeAmount: 20 },
    ]);
    vi.mocked(posApi.addLine).mockResolvedValue(makeCart());
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Meal Deal/ }));

    const dialog = await screen.findByRole('dialog', { name: 'Meal Deal' });
    const add = await within(dialog).findByRole('button', { name: 'Add to cart' });
    expect(add).toBeDisabled();
    expect(within(dialog).getByText(/can add ₱20\.00/)).toBeInTheDocument();

    fireEvent.change(within(dialog).getByLabelText('Drink 1'), { target: { value: 'latte' } });
    expect(add).toBeDisabled();
    fireEvent.change(within(dialog).getByLabelText('Drink 2'), { target: { value: 'tea' } });
    expect(add).toBeEnabled();
    fireEvent.click(add);

    await waitFor(() =>
      expect(posApi.addLine).toHaveBeenCalledWith({
        itemId: 'meal',
        itemVariantId: null,
        quantity: 1,
        comboSelections: [
          { slotId: 'slot1', selectedItemId: 'latte' },
          { slotId: 'slot1', selectedItemId: 'tea' },
        ],
      }),
    );
  });

  it('adds an item when its barcode is scanned and Enter is pressed', async () => {
    vi.mocked(posApi.addLine).mockResolvedValue(makeCart());
    renderPage(<SellPage />);
    await screen.findByRole('button', { name: /Iced Latte/ });
    const search = screen.getByLabelText('Search items or scan a barcode');
    fireEvent.change(search, { target: { value: '4800001' } });
    fireEvent.keyDown(search, { key: 'Enter' });
    await waitFor(() => expect(posApi.addLine).toHaveBeenCalledWith({ itemId: 'latte', itemVariantId: null, quantity: 1 }));
    expect(search).toHaveValue('');
  });

  it('tells the cashier when a scanned code matches nothing', async () => {
    renderPage(<SellPage />);
    await screen.findByRole('button', { name: /Iced Latte/ });
    const search = screen.getByLabelText('Search items or scan a barcode');
    fireEvent.change(search, { target: { value: '0000' } });
    fireEvent.keyDown(search, { key: 'Enter' });
    expect(useToastStore.getState().toasts[0].message).toBe('No item with that barcode or SKU');
    expect(posApi.addLine).not.toHaveBeenCalled();
  });

  it('sends a weight for a by-weight item', async () => {
    vi.mocked(catalogApi.listItems).mockResolvedValue([makeItem({ id: 'rice', name: 'Rice', pricingType: PricingType.WeightVolume, basePrice: 55 })]);
    vi.mocked(posApi.addLine).mockResolvedValue(makeCart());
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Rice/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Rice' });
    expect(within(dialog).getByRole('button', { name: 'Add to cart' })).toBeDisabled();
    fireEvent.change(within(dialog).getByLabelText('Weight or amount'), { target: { value: '0.35' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add to cart' }));
    await waitFor(() => expect(posApi.addLine).toHaveBeenCalledWith({ itemId: 'rice', itemVariantId: null, quantity: 0.35 }));
  });
});

describe('cart actions', () => {
  const twoLines = () =>
    makeCart({
      lines: [makeLine({ id: 'l1', itemName: 'Iced Latte', quantity: 2, lineTotal: 300 }), makeLine({ id: 'l2', itemName: 'Rice', quantity: 0.35, lineTotal: 19 })],
      subtotal: 319,
      totalAmount: 319,
    });

  it('changes quantity and removes a line through the server', async () => {
    vi.mocked(posApi.getCart).mockResolvedValue(twoLines());
    vi.mocked(posApi.updateLine).mockResolvedValue(twoLines());
    vi.mocked(posApi.removeLine).mockResolvedValue(makeCart());
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Increase Iced Latte' }));
    await waitFor(() => expect(posApi.updateLine).toHaveBeenCalledWith('l1', 3));

    fireEvent.click(screen.getAllByRole('button', { name: 'Remove' })[1]);
    await waitFor(() => expect(posApi.removeLine).toHaveBeenCalledWith('l2'));
  });

  it('does not offer plus and minus on a fractional weight line', async () => {
    vi.mocked(posApi.getCart).mockResolvedValue(twoLines());
    renderPage(<SellPage />);
    expect(await screen.findByRole('button', { name: 'Increase Rice' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Decrease Rice' })).toBeDisabled();
  });

  it('applies a promo code in upper case and sets the order type', async () => {
    vi.mocked(posApi.applyPromoCode).mockResolvedValue(makeCart());
    vi.mocked(posApi.setOrderType).mockResolvedValue(makeCart({ orderType: 'Take Out' }));
    renderPage(<SellPage />);
    fireEvent.change(await screen.findByLabelText('Promo code'), { target: { value: 'summer10' } });
    fireEvent.click(screen.getByRole('button', { name: 'Apply' }));
    await waitFor(() => expect(posApi.applyPromoCode).toHaveBeenCalledWith('SUMMER10'));

    fireEvent.click(screen.getByRole('button', { name: 'Take Out' }));
    await waitFor(() => expect(posApi.setOrderType).toHaveBeenCalledWith('Take Out'));
  });

  it('lets a cashier see but not use Senior/PWD, and gives no clear-cart button', async () => {
    vi.mocked(posApi.getCart).mockResolvedValue(twoLines());
    renderPage(<SellPage />);
    expect(await screen.findByRole('switch')).toBeDisabled();
    expect(screen.getByText('A manager applies this')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clear cart' })).not.toBeInTheDocument();
  });

  it('lets a manager apply Senior/PWD and clear the cart after confirming', async () => {
    signInAs('Manager', driveSession);
    vi.mocked(posApi.getCart).mockResolvedValue(twoLines());
    vi.mocked(posApi.applySeniorPwd).mockResolvedValue(twoLines());
    vi.mocked(posApi.voidCart).mockResolvedValue(makeCart());
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('switch'));
    await waitFor(() => expect(posApi.applySeniorPwd).toHaveBeenCalledWith(true));

    fireEvent.click(screen.getByRole('button', { name: 'Clear cart' }));
    const dialog = screen.getByRole('dialog', { name: 'Clear the cart?' });
    expect(posApi.voidCart).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Clear cart' }));
    await waitFor(() => expect(posApi.voidCart).toHaveBeenCalledTimes(1));
  });

  it('disables charging an empty cart', async () => {
    renderPage(<SellPage />);
    expect(await screen.findByRole('button', { name: /Charge/ })).toBeDisabled();
  });

  it('shows a retryable error when the cart cannot load', async () => {
    vi.mocked(posApi.getCart).mockRejectedValue(new Error('boom'));
    renderPage(<SellPage />);
    expect(await screen.findByText('The cart could not be loaded')).toBeInTheDocument();
  });
});
