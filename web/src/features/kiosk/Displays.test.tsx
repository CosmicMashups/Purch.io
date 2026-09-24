import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { makeCart, makeLine } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { KitchenStatus } from '../pos/types';
import { displayApi } from './api';
import { KitchenDisplayPage } from './KitchenDisplayPage';
import { OrderBoardPage } from './OrderBoardPage';

vi.mock('./api', () => ({
  deviceApi: { pair: vi.fn() },
  kioskApi: {},
  displayApi: { pending: vi.fn(), setKitchenStatus: vi.fn() },
}));

const orders = [
  makeCart({
    id: 'a',
    kioskPrepNumber: 12,
    orderType: 'Dine In',
    kitchenStatus: KitchenStatus.Queued,
    lines: [makeLine({ id: 'l1', itemName: 'Burger', quantity: 2, modifierSelections: [{ itemModifierId: 'm', modifierName: 'No onions', modifierGroupName: 'Extras', priceDelta: 0 }] })],
  }),
  makeCart({ id: 'b', kioskPrepNumber: 11, kitchenStatus: KitchenStatus.Ready, lines: [makeLine({ id: 'l2', itemName: 'Fries' })] }),
];

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(displayApi.pending).mockResolvedValue(orders);
});

describe('KitchenDisplayPage', () => {
  beforeEach(() => signInAs('KitchenDisplay', { device_id: 'kd', branch_id: 'b1' }));

  it('asks for this branch only, from the token', async () => {
    renderPage(<KitchenDisplayPage />);
    await screen.findByText('#12');
    expect(displayApi.pending).toHaveBeenCalledWith('KitchenDisplay', 'b1');
  });

  it('shows tickets oldest first with what to make and how', async () => {
    renderPage(<KitchenDisplayPage />);
    await screen.findByText('#12');
    const tickets = screen.getAllByRole('listitem').filter((li) => /#\d+/.test(li.textContent ?? '') && li.querySelector('button'));
    expect(tickets[0]).toHaveTextContent('#11');
    expect(tickets[1]).toHaveTextContent('#12');
    expect(tickets[1]).toHaveTextContent('2 × Burger');
    expect(tickets[1]).toHaveTextContent('No onions');
  });

  it('moves a ticket to the next status', async () => {
    vi.mocked(displayApi.setKitchenStatus).mockResolvedValue(makeCart());
    renderPage(<KitchenDisplayPage />);
    await screen.findByText('#12');
    fireEvent.click(screen.getByRole('button', { name: 'Start preparing' }));
    await waitFor(() => expect(displayApi.setKitchenStatus).toHaveBeenCalledWith('a', KitchenStatus.Preparing));
  });

  it('offers the last step on a ready ticket', async () => {
    renderPage(<KitchenDisplayPage />);
    await screen.findByText('#11');
    expect(screen.getByRole('button', { name: 'Picked up' })).toBeInTheDocument();
  });

  it('says so when there are no orders', async () => {
    vi.mocked(displayApi.pending).mockResolvedValue([]);
    renderPage(<KitchenDisplayPage />);
    expect(await screen.findByText('No pending orders')).toBeInTheDocument();
  });

  it('refuses to guess a branch when the token has none', () => {
    signInAs('KitchenDisplay', { device_id: 'kd' });
    renderPage(<KitchenDisplayPage />);
    expect(screen.getByText('This screen has no branch')).toBeInTheDocument();
    expect(displayApi.pending).not.toHaveBeenCalled();
  });
});

describe('OrderBoardPage', () => {
  beforeEach(() => signInAs('OrderBoard', { device_id: 'ob', branch_id: 'b1' }));

  it('splits ready orders from those being prepared, by number only', async () => {
    renderPage(<OrderBoardPage />);
    const ready = await screen.findByRole('region', { name: 'Ready for pickup' });
    const preparing = screen.getByRole('region', { name: 'Preparing' });
    expect(within(ready).getByText('11')).toBeInTheDocument();
    expect(within(preparing).getByText('12')).toBeInTheDocument();
    expect(displayApi.pending).toHaveBeenCalledWith('OrderBoard', 'b1');
  });

  it('never shows what anyone ordered', async () => {
    renderPage(<OrderBoardPage />);
    await screen.findByText('12');
    expect(screen.queryByText(/Burger|Fries|onions/)).toBeNull();
  });

  it('shows a plain error, with retry, when orders cannot be loaded at all', async () => {
    vi.mocked(displayApi.pending).mockRejectedValue(new Error('net'));
    renderPage(<OrderBoardPage />);
    expect(await screen.findByText('Orders could not be loaded')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again|retry/i })).toBeInTheDocument();
  });
});
