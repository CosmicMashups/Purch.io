import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Toaster } from '../../../components/feedback/Toaster';
import { toast, useToastStore } from '../../../components/feedback/toastStore';
import { ApiError } from '../../../lib/apiError';
import { makeCart, makeLine } from '../../../test/pos';
import { renderPage, signInAs } from '../../../test/render';
import { catalogApi } from '../../catalog/api';
import { formatPeso } from '../../dashboard/format';
import { posApi } from '../api';
import { posAddQueue } from '../queries';
import { SellPage } from '../SellPage';
import type { ParkedCartSummary } from '../types';
import { ParkedCartsDrawer } from './ParkedCartsDrawer';

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

const driveSession = { device_id: 'dev1', branch_id: 'kat', scope_type: 'Branch', scope_id: 'kat' };
const fullCart = makeCart({ lines: [makeLine({ id: 'l1', itemName: 'Iced Latte', lineTotal: 150 })], subtotal: 150, totalAmount: 150 });
const minutesAgo = (m: number) => new Date(Date.now() - m * 60_000).toISOString();

const parked: ParkedCartSummary[] = [
  { id: 'p1aaaaaa-0000-0000-0000-000000000000', label: 'Table 4', parkedAt: minutesAgo(5), parkedByName: 'Rosa Dimaculangan', lineCount: 3, totalAmount: 425.5 },
  { id: 'abcd1234-ffff-0000-0000-000000000000', label: null, parkedAt: minutesAgo(30), parkedByName: 'Jun Pascual', lineCount: 0, totalAmount: 0 },
];

const pinRefusal = () => new ApiError('validation', 'An Admin or Manager PIN is required to approve this.', { approverPin: ['An Admin or Manager PIN is required to approve this.'] });
const toastMessages = () => useToastStore.getState().toasts.map((t) => t.message);

beforeEach(() => {
  vi.clearAllMocks();
  posAddQueue.reset();
  useToastStore.setState({ toasts: [] });
  signInAs('Cashier', driveSession);
  vi.mocked(catalogApi.listItems).mockResolvedValue([]);
  vi.mocked(catalogApi.listCategories).mockResolvedValue([]);
  vi.mocked(catalogApi.listModifierGroups).mockResolvedValue([]);
  vi.mocked(posApi.getCart).mockResolvedValue(fullCart);
  vi.mocked(posApi.listParked).mockResolvedValue(parked);
});

afterEach(() => vi.restoreAllMocks());

async function openDrawer() {
  renderPage(<SellPage />);
  fireEvent.click(await screen.findByRole('button', { name: /Parked carts, 2 waiting/ }));
  return screen.findByRole('dialog', { name: 'Parked carts' });
}

describe('Park cart', () => {
  it('is disabled while the cart is empty', async () => {
    vi.mocked(posApi.getCart).mockResolvedValue(makeCart());
    renderPage(<SellPage />);
    expect(await screen.findByRole('button', { name: 'Park cart' })).toBeDisabled();
  });

  it('asks for an optional label, parks the cart, and the register starts a fresh one (as a cashier)', async () => {
    renderPage(<SellPage />);
    await screen.findByTestId('cart-total');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Park cart' })).toBeEnabled());
    fireEvent.click(screen.getByRole('button', { name: 'Park cart' }));

    const dialog = await screen.findByRole('dialog', { name: 'Park this cart' });
    const label = within(dialog).getByLabelText(/Label/);
    expect(label).toHaveAttribute('maxLength', '80');
    fireEvent.change(label, { target: { value: '  Table 4 ' } });
    vi.mocked(posApi.parkCart).mockResolvedValue(undefined);
    vi.mocked(posApi.getCart).mockResolvedValue(makeCart({ id: 'cart2' }));
    fireEvent.click(within(dialog).getByRole('button', { name: 'Park cart' }));

    await waitFor(() => expect(posApi.parkCart).toHaveBeenCalledWith('Table 4'));
    expect(await screen.findByText('The cart is empty. Tap an item to add it.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'Park this cart' })).not.toBeInTheDocument();
    expect(toastMessages()).toContain('Cart parked as "Table 4"');
  });

  it('sends no label when it is left blank', async () => {
    renderPage(<SellPage />);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Park cart' })).toBeEnabled());
    fireEvent.click(screen.getByRole('button', { name: 'Park cart' }));
    const dialog = await screen.findByRole('dialog', { name: 'Park this cart' });
    vi.mocked(posApi.parkCart).mockResolvedValue(undefined);
    fireEvent.click(within(dialog).getByRole('button', { name: 'Park cart' }));
    await waitFor(() => expect(posApi.parkCart).toHaveBeenCalledWith(undefined));
  });
});

describe('Parked carts drawer', () => {
  it('lists parked carts with label, time, who parked it, line count and total', async () => {
    const drawer = await openDrawer();
    const rows = await within(drawer).findAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(within(rows[0]).getByText('Table 4')).toBeInTheDocument();
    expect(within(rows[0]).getByText(/5 min ago/)).toBeInTheDocument();
    expect(within(rows[0]).getByText(/Rosa Dimaculangan/)).toBeInTheDocument();
    expect(within(rows[0]).getByText(/3 items/)).toBeInTheDocument();
    expect(within(rows[0]).getByText(formatPeso(425.5))).toBeInTheDocument();
    expect(within(rows[1]).getByText('Cart abcd1234')).toBeInTheDocument();
    expect(within(drawer).getByText(/parks the cart you have open now/)).toBeInTheDocument();
    expect(within(rows[0]).getByRole('button', { name: 'Resume Table 4' })).toBeInTheDocument();
    expect(within(rows[0]).getByRole('button', { name: 'Discard Table 4' })).toBeInTheDocument();
  });

  it('shows an empty state when nothing is parked', async () => {
    vi.mocked(posApi.listParked).mockResolvedValue([]);
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Parked carts, none waiting' }));
    expect(await screen.findByText('No parked carts')).toBeInTheDocument();
  });

  it('shows an error with a retry when the list fails', async () => {
    vi.mocked(posApi.listParked).mockRejectedValue(new ApiError('network', 'Could not reach the server.'));
    renderPage(<SellPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Parked carts/ }));
    expect(await screen.findByText('Parked carts could not be loaded')).toBeInTheDocument();
  });

  it('resumes a cart and summarises what changed in a toast', async () => {
    vi.mocked(posApi.resumeCart).mockResolvedValue(
      makeCart({
        id: 'p1',
        lines: [makeLine({ id: 'a', itemName: 'Mocha' })],
        totalAmount: 100,
        changedLines: [
          { lineId: 'a', reason: 'PriceChanged' },
          { lineId: 'b', reason: 'PriceChanged' },
          { lineId: 'c', reason: 'Unavailable' },
          { lineId: 'd', reason: 'NotRepriced' },
        ],
      }),
    );
    const drawer = await openDrawer();
    fireEvent.click(await within(drawer).findByRole('button', { name: 'Resume Table 4' }));
    await waitFor(() => expect(posApi.resumeCart).toHaveBeenCalledWith(parked[0].id));
    await waitFor(() =>
      expect(toastMessages()).toContain('Resumed Table 4. 2 prices changed, 1 unavailable item removed, 1 line kept its old price.'),
    );
    expect(screen.queryByRole('dialog', { name: 'Parked carts' })).not.toBeInTheDocument();
  });

  it('says plainly when a resumed cart came back unchanged', async () => {
    vi.mocked(posApi.resumeCart).mockResolvedValue(makeCart({ id: 'p1', changedLines: [] }));
    const drawer = await openDrawer();
    fireEvent.click(await within(drawer).findByRole('button', { name: 'Resume Table 4' }));
    await waitFor(() => expect(toastMessages()).toContain('Resumed Table 4'));
  });

  it('discards an empty parked cart after a confirm, with no PIN', async () => {
    vi.mocked(posApi.discardParked).mockResolvedValue(undefined);
    const drawer = await openDrawer();
    fireEvent.click(await within(drawer).findByRole('button', { name: 'Discard Cart abcd1234' }));
    const confirm = await screen.findByRole('dialog', { name: 'Discard Cart abcd1234?' });
    fireEvent.click(within(confirm).getByRole('button', { name: 'Discard' }));
    await waitFor(() => expect(posApi.discardParked).toHaveBeenCalledWith(parked[1].id, undefined));
    expect(screen.queryByRole('dialog', { name: 'Manager or admin PIN' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Manager or admin PIN')).not.toBeInTheDocument();
  });

  it('asks for an approver PIN to discard a parked cart with a total', async () => {
    vi.mocked(posApi.discardParked).mockImplementation((_id, pin) => (pin === '5678' ? Promise.resolve(undefined) : Promise.reject(pinRefusal())));
    const drawer = await openDrawer();
    fireEvent.click(await within(drawer).findByRole('button', { name: 'Discard Table 4' }));
    const confirm = await screen.findByRole('dialog', { name: 'Discard Table 4?' });
    expect(within(confirm).getByText(/manager or admin PIN/)).toBeInTheDocument();
    fireEvent.click(within(confirm).getByRole('button', { name: 'Discard' }));

    fireEvent.change(await screen.findByLabelText('Manager or admin PIN'), { target: { value: '5678' } });
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(posApi.discardParked).toHaveBeenLastCalledWith(parked[0].id, '5678'));
    await waitFor(() => expect(toastMessages()).toContain('Discarded Table 4'));
  });

  it.each([
    ['conflict', 'Another register already resumed this cart'],
    ['notFound', 'This parked cart is no longer available'],
    ['forbidden', "You can't change this parked cart."],
  ] as const)('a %s on resume says why in plain words and refetches the list', async (kind, message) => {
    vi.mocked(posApi.resumeCart).mockRejectedValue(new ApiError(kind, 'raw server text'));
    const drawer = await openDrawer();
    await within(drawer).findByRole('button', { name: 'Resume Table 4' });
    const before = vi.mocked(posApi.listParked).mock.calls.length;
    fireEvent.click(within(drawer).getByRole('button', { name: 'Resume Table 4' }));
    await waitFor(() => expect(toastMessages()).toContain(message));
    await waitFor(() => expect(vi.mocked(posApi.listParked).mock.calls.length).toBeGreaterThan(before));
  });

  it('a stale discard says why and refetches the list', async () => {
    vi.mocked(posApi.discardParked).mockRejectedValue(new ApiError('notFound', 'raw'));
    const drawer = await openDrawer();
    fireEvent.click(await within(drawer).findByRole('button', { name: 'Discard Cart abcd1234' }));
    const before = vi.mocked(posApi.listParked).mock.calls.length;
    fireEvent.click(within(await screen.findByRole('dialog', { name: 'Discard Cart abcd1234?' })).getByRole('button', { name: 'Discard' }));
    await waitFor(() => expect(toastMessages()).toContain('This parked cart is no longer available'));
    await waitFor(() => expect(vi.mocked(posApi.listParked).mock.calls.length).toBeGreaterThan(before));
  });

  it.each(['Manager', 'Admin'])('a %s discards a zero-total cart with no PIN', async (role) => {
    signInAs(role, driveSession);
    vi.mocked(posApi.discardParked).mockResolvedValue(undefined);
    const drawer = await openDrawer();
    fireEvent.click(await within(drawer).findByRole('button', { name: 'Discard Cart abcd1234' }));
    fireEvent.click(within(await screen.findByRole('dialog', { name: 'Discard Cart abcd1234?' })).getByRole('button', { name: 'Discard' }));
    await waitFor(() => expect(posApi.discardParked).toHaveBeenCalledWith(parked[1].id, undefined));
    expect(screen.queryByLabelText('Manager or admin PIN')).not.toBeInTheDocument();
  });

  it('keeps rows disabled while the PIN dialog is open', async () => {
    vi.mocked(posApi.discardParked).mockRejectedValue(pinRefusal());
    const drawer = await openDrawer();
    fireEvent.click(await within(drawer).findByRole('button', { name: 'Discard Table 4' }));
    fireEvent.click(within(await screen.findByRole('dialog', { name: 'Discard Table 4?' })).getByRole('button', { name: 'Discard' }));
    await screen.findByLabelText('Manager or admin PIN');
    expect(within(drawer).getByRole('button', { name: 'Resume Table 4' })).toBeDisabled();
    expect(within(drawer).getByRole('button', { name: 'Discard Cart abcd1234' })).toBeDisabled();
  });

  it('Escape closes the confirm on top, not the drawer', async () => {
    const drawer = await openDrawer();
    fireEvent.click(await within(drawer).findByRole('button', { name: 'Discard Table 4' }));
    await screen.findByRole('dialog', { name: 'Discard Table 4?' });
    fireEvent.keyDown(window, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Discard Table 4?' })).not.toBeInTheDocument());
    expect(screen.getByRole('dialog', { name: 'Parked carts' })).toBeInTheDocument();
    fireEvent.keyDown(window, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Parked carts' })).not.toBeInTheDocument());
  });

  it('starts on Close, keeps Tab inside the drawer, and makes the till inert', async () => {
    const drawer = await openDrawer();
    const close = within(drawer).getByRole('button', { name: 'Close parked carts' });
    await waitFor(() => expect(close).toHaveFocus());
    const last = await within(drawer).findByRole('button', { name: 'Resume Cart abcd1234' });
    last.focus();
    fireEvent.keyDown(window, { key: 'Tab' });
    expect(close).toHaveFocus();
    fireEvent.keyDown(window, { key: 'Tab', shiftKey: true });
    expect(last).toHaveFocus();
    expect(screen.getByTestId('cart-total').closest('[inert]')).not.toBeNull();
  });

  it('a 403 on discard shows the copy, not the PIN dialog, and keeps network errors their own wording', async () => {
    vi.mocked(posApi.discardParked).mockRejectedValueOnce(new ApiError('forbidden', 'raw')).mockRejectedValueOnce(new ApiError('network', 'raw'));
    const drawer = await openDrawer();
    for (const expected of ["You can't change this parked cart.", 'Cannot reach the server. Check the connection and try again.']) {
      fireEvent.click(await within(drawer).findByRole('button', { name: 'Discard Table 4' }));
      fireEvent.click(within(await screen.findByRole('dialog', { name: 'Discard Table 4?' })).getByRole('button', { name: 'Discard' }));
      await waitFor(() => expect(toastMessages()).toContain(expected));
      expect(screen.queryByLabelText('Manager or admin PIN')).not.toBeInTheDocument();
      await waitFor(() => expect(within(drawer).getByRole('button', { name: 'Discard Table 4' })).toBeEnabled());
    }
  });

  it('keeps toasts live while open, and lifts inert from the till after close and after unmount', async () => {
    const view = renderPage(
      <>
        <SellPage />
        <Toaster />
      </>,
    );
    fireEvent.click(await screen.findByRole('button', { name: /Parked carts, 2 waiting/ }));
    const drawer = await screen.findByRole('dialog', { name: 'Parked carts' });
    act(() => toast.success('Discarded Table 4'));
    const shown = await screen.findByText('Discarded Table 4');
    expect(shown.closest('[inert]')).toBeNull();
    expect(screen.getByTestId('cart-total').closest('[inert]')).not.toBeNull();

    fireEvent.click(within(drawer).getByRole('button', { name: 'Close parked carts' }));
    await waitFor(() => expect(screen.getByTestId('cart-total').closest('[inert]')).toBeNull());

    fireEvent.click(screen.getByRole('button', { name: /Parked carts, 2 waiting/ }));
    await screen.findByRole('dialog', { name: 'Parked carts' });
    view.unmount();
    expect(document.querySelectorAll('[inert]')).toHaveLength(0);
    expect(document.body.style.overflow).toBe('');
  });
});

describe('Parked carts drawer offline', () => {
  it('pauses with the needs-a-connection copy instead of loading the list', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    renderPage(<ParkedCartsDrawer open onClose={() => {}} cartHasItems={false} />);
    const drawer = screen.getByRole('dialog', { name: 'Parked carts' });
    expect(within(drawer).getByText('Selling needs a connection')).toBeInTheDocument();
    expect(posApi.listParked).not.toHaveBeenCalled();
  });
});
