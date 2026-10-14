import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../lib/apiError';
import { makeCart, makeLine } from '../../../test/pos';
import { renderPage, signInAs } from '../../../test/render';
import { catalogApi } from '../../catalog/api';
import { tenantApi } from '../../tenant/api';
import type { TenantSettings } from '../../tenant/types';
import { posApi } from '../api';
import { posAddQueue } from '../queries';
import { SellPage } from '../SellPage';

vi.mock('../api', () => ({
  posApi: {
    getCart: vi.fn(),
    addLines: vi.fn(),
    updateLine: vi.fn(),
    removeLine: vi.fn(),
    voidCart: vi.fn(),
    applyPromoCode: vi.fn(),
    applySeniorPwd: vi.fn(),
    setOrderType: vi.fn(),
    parkCart: vi.fn(),
    listParked: vi.fn(),
    resumeCart: vi.fn(),
    discardParked: vi.fn(),
    setLineDiscount: vi.fn(),
    setCartDiscount: vi.fn(),
    overrideLinePrice: vi.fn(),
    addOpenItem: vi.fn(),
    setLineNote: vi.fn(),
  },
}));
vi.mock('../../catalog/api', () => ({
  catalogApi: {
    listItems: vi.fn(),
    listCategories: vi.fn(),
    listModifierGroups: vi.fn(),
    listItemModifierGroups: vi.fn(),
    listVariants: vi.fn(),
    listComboComponents: vi.fn(),
  },
}));
vi.mock('../../tenant/api', () => ({ tenantApi: { get: vi.fn() } }));

const session = { device_id: 'dev1', branch_id: 'kat', scope_type: 'Branch', scope_id: 'kat' };
const latte = makeLine({ id: 'l1', itemName: 'Iced Latte', unitPrice: 150, lineTotal: 150 });
const cart = makeCart({ lines: [latte], subtotal: 150, totalAmount: 150 });
const settings = (openItemEnabled: boolean) => ({ openItemEnabled }) as TenantSettings;

beforeEach(() => {
  vi.clearAllMocks();
  posAddQueue.reset();
  vi.mocked(catalogApi.listItems).mockResolvedValue([]);
  vi.mocked(catalogApi.listCategories).mockResolvedValue([]);
  vi.mocked(catalogApi.listModifierGroups).mockResolvedValue([]);
  vi.mocked(posApi.listParked).mockResolvedValue([]);
  vi.mocked(posApi.getCart).mockResolvedValue(cart);
  vi.mocked(tenantApi.get).mockResolvedValue(settings(true));
});

async function openLineMenu(name = 'Iced Latte') {
  fireEvent.click(await screen.findByRole('button', { name: `Actions for ${name}` }));
  return screen.getByRole('menu', { name: `Actions for ${name}` });
}

describe('role rules', () => {
  it('hides discount and override from a Cashier', async () => {
    signInAs('Cashier', session);
    renderPage(<SellPage />);
    const menu = await openLineMenu();
    expect(within(menu).queryByRole('menuitem', { name: /Discount/ })).toBeNull();
    expect(within(menu).queryByRole('menuitem', { name: /Override price/ })).toBeNull();
    expect(within(menu).getByRole('menuitem', { name: /Add note/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add discount' })).toBeNull();
  });

  it.each(['Manager', 'Admin'])('shows discount and override to a %s', async (role) => {
    signInAs(role, session);
    renderPage(<SellPage />);
    const menu = await openLineMenu();
    expect(within(menu).getByRole('menuitem', { name: /Discount/ })).toBeEnabled();
    expect(within(menu).getByRole('menuitem', { name: /Override price/ })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Add discount' })).toBeInTheDocument();
  });
});

describe('Senior/PWD exclusivity', () => {
  it('disables manual discounts with an explanation while Senior/PWD is applied', async () => {
    signInAs('Manager', session);
    vi.mocked(posApi.getCart).mockResolvedValue({ ...cart, seniorPwdDiscountApplied: true });
    renderPage(<SellPage />);
    const menu = await openLineMenu();
    expect(within(menu).getByRole('menuitem', { name: /Discount/ })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Add discount' })).toBeNull();
    expect(screen.getAllByText(/Senior\/PWD discount is applied - remove it to give a manual discount/).length).toBeGreaterThan(0);
  });

  it('confirms before Senior/PWD clears an existing manual discount', async () => {
    signInAs('Manager', session);
    vi.mocked(posApi.getCart).mockResolvedValue({ ...cart, manualDiscountAmount: 15, manualDiscountKind: 'Fixed', manualDiscountValue: 15, manualDiscountReason: 'Regular' });
    vi.mocked(posApi.applySeniorPwd).mockResolvedValue({ ...cart, seniorPwdDiscountApplied: true });
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('switch'));
    const dialog = await screen.findByRole('dialog', { name: /Apply Senior\/PWD/ });
    expect(posApi.applySeniorPwd).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Apply Senior/PWD' }));
    await waitFor(() => expect(posApi.applySeniorPwd).toHaveBeenCalledWith(true));
  });
});

describe('line display', () => {
  it('shows the manual discount and the cart discount row', async () => {
    signInAs('Manager', session);
    vi.mocked(posApi.getCart).mockResolvedValue({
      ...cart,
      lines: [{ ...latte, manualDiscountKind: 'Percent', manualDiscountValue: 10, manualDiscountAmount: 15 }],
      manualDiscountAmount: 5,
      manualDiscountKind: 'Fixed',
      manualDiscountValue: 5,
      manualDiscountReason: 'Regular',
    });
    renderPage(<SellPage />);
    expect(await screen.findByText(/-10% \(-₱\s?15\.00\)/)).toBeInTheDocument();
    expect(screen.getByText(/Cart discount/)).toBeInTheDocument();
  });

  it("shows an overridden price with what it was", async () => {
    signInAs('Manager', session);
    vi.mocked(posApi.getCart).mockResolvedValue({ ...cart, lines: [{ ...latte, unitPrice: 120, lineTotal: 120, priceOverrideFrom: 150 }] });
    renderPage(<SellPage />);
    expect(await screen.findByText(/was ₱\s?150\.00/)).toBeInTheDocument();
  });

  it('shows an open item with its tag', async () => {
    signInAs('Cashier', session);
    vi.mocked(posApi.getCart).mockResolvedValue({ ...cart, lines: [{ ...latte, itemName: 'Gift wrap', isOpenItem: true, openItemName: 'Gift wrap' }] });
    renderPage(<SellPage />);
    expect(await screen.findByText('Open item')).toBeInTheDocument();
  });
});

describe('open item', () => {
  it('is hidden when the tenant has it turned off', async () => {
    signInAs('Cashier', session);
    vi.mocked(tenantApi.get).mockResolvedValue(settings(false));
    renderPage(<SellPage />);
    await screen.findByRole('button', { name: 'Actions for Iced Latte' });
    await waitFor(() => expect(tenantApi.get).toHaveBeenCalled());
    expect(screen.queryByRole('button', { name: /Open item/ })).toBeNull();
  });

  it('adds the item with the right body', async () => {
    signInAs('Cashier', session);
    vi.mocked(posApi.addOpenItem).mockResolvedValue(cart);
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Open item/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Add an open item' });
    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: 'Gift wrap' } });
    fireEvent.change(within(dialog).getByLabelText('Price'), { target: { value: '25' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add to cart' }));
    await waitFor(() => expect(posApi.addOpenItem).toHaveBeenCalledWith({ name: 'Gift wrap', unitPrice: 25, quantity: 1, note: undefined, approverPin: undefined }));
  });
});

describe('line note', () => {
  it('saves a note and shows it on the line', async () => {
    signInAs('Cashier', session);
    vi.mocked(posApi.setLineNote).mockResolvedValue({ ...cart, lines: [{ ...latte, note: 'Less ice' }] });
    renderPage(<SellPage />);
    const menu = await openLineMenu();
    fireEvent.click(within(menu).getByRole('menuitem', { name: /Add note/ }));
    const dialog = await screen.findByRole('dialog', { name: /Note for Iced Latte/ });
    fireEvent.change(within(dialog).getByLabelText('Note'), { target: { value: 'Less ice' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save note' }));
    await waitFor(() => expect(posApi.setLineNote).toHaveBeenCalledWith('l1', 'Less ice', undefined));
    expect(await screen.findByText('Less ice')).toBeInTheDocument();
  });

  it('asks for a PIN on a claimed kiosk order and retries with it', async () => {
    signInAs('Cashier', session);
    vi.mocked(posApi.setLineNote)
      .mockRejectedValueOnce(new ApiError('validation', 'PIN needed', { approverPin: ['An Admin or Manager PIN is required to approve this.'] }))
      .mockResolvedValueOnce({ ...cart, lines: [{ ...latte, note: 'Less ice' }] });
    renderPage(<SellPage />);
    const menu = await openLineMenu();
    fireEvent.click(within(menu).getByRole('menuitem', { name: /Add note/ }));
    const dialog = await screen.findByRole('dialog', { name: /Note for Iced Latte/ });
    fireEvent.change(within(dialog).getByLabelText('Note'), { target: { value: 'Less ice' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save note' }));
    const pin = await screen.findByText(/An Admin or Manager PIN is required/);
    const pinDialog = pin.closest('[role="dialog"]') as HTMLElement;
    fireEvent.change(within(pinDialog).getByLabelText(/PIN/), { target: { value: '1234' } });
    fireEvent.submit(within(pinDialog).getByLabelText(/PIN/).closest('form') as HTMLFormElement);
    await waitFor(() => expect(posApi.setLineNote).toHaveBeenLastCalledWith('l1', 'Less ice', '1234'));
  });
});
