import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { branchesApi } from '../../branches/api';
import { catalogApi } from '../../catalog/api';
import type { Item } from '../../catalog/types';
import { inventoryApi } from '../api';
import type { InventoryMovement } from '../types';
import { MovementLogPage } from './MovementLogPage';

vi.mock('../../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../../catalog/api', () => ({ catalogApi: { listItems: vi.fn() } }));
vi.mock('../api', () => ({ MOVEMENT_PAGE_SIZE: 2, inventoryApi: { listMovements: vi.fn() } }));

const movement = (id: string, over: Partial<InventoryMovement> = {}): InventoryMovement => ({
  id,
  itemId: 'latte',
  itemName: `Latte ${id}`,
  branchId: 'kat',
  branchName: 'Katipunan',
  type: 0,
  quantity: 5,
  staffUserId: 's',
  staffUserName: 'Ana',
  note: null,
  reasonCategory: null,
  photoUrl: null,
  supplierReference: null,
  createdAt: `2026-09-2${id}T02:00:00Z`,
  ...over,
});

describe('MovementLogPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Manager');
    vi.mocked(catalogApi.listItems).mockResolvedValue([{ id: 'latte', name: 'Latte' }] as Item[]);
    vi.mocked(branchesApi.list).mockResolvedValue([{ id: 'kat', name: 'Katipunan', address: null }]);
  });

  it('lists movements and marks automated sales', async () => {
    vi.mocked(inventoryApi.listMovements).mockResolvedValue([movement('1'), movement('2', { type: 8, itemName: 'Cookie' })]);
    renderPage(<MovementLogPage />);
    expect(await screen.findByText('Latte 1')).toBeInTheDocument();
    expect(screen.getByText('Automatic')).toBeInTheDocument();
  });

  it('pages older movements with the last row as the cursor', async () => {
    vi.mocked(inventoryApi.listMovements)
      .mockResolvedValueOnce([movement('1'), movement('2')])
      .mockResolvedValueOnce([movement('3')]);
    renderPage(<MovementLogPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Load older movements' }));

    await waitFor(() => expect(inventoryApi.listMovements).toHaveBeenCalledTimes(2));
    expect(vi.mocked(inventoryApi.listMovements).mock.calls[1][1]).toEqual({ before: '2026-09-22T02:00:00Z', beforeId: '2' });
    expect(await screen.findByText('Latte 3')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Load older movements' })).not.toBeInTheDocument();
  });

  it('filters by type from the URL and by picking a type', async () => {
    vi.mocked(inventoryApi.listMovements).mockResolvedValue([]);
    renderPage(<MovementLogPage />, { route: '/?type=3' });
    await screen.findByText('No movements match these filters.');
    expect(vi.mocked(inventoryApi.listMovements).mock.calls[0][0]).toMatchObject({ type: 3 });

    fireEvent.change(screen.getByLabelText('Type'), { target: { value: '5' } });
    await waitFor(() => expect(vi.mocked(inventoryApi.listMovements).mock.calls.at(-1)?.[0]).toMatchObject({ type: 5 }));
  });

  it('shows a retryable error instead of an empty log', async () => {
    vi.mocked(inventoryApi.listMovements).mockRejectedValue(new Error('boom'));
    renderPage(<MovementLogPage />);
    expect(await screen.findByText('The movement log could not be loaded')).toBeInTheDocument();
    expect(screen.queryByText('No movements match these filters.')).not.toBeInTheDocument();
  });
});
