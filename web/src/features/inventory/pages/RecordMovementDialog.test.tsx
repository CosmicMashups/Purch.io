import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { branchesApi } from '../../branches/api';
import { catalogApi } from '../../catalog/api';
import type { Item } from '../../catalog/types';
import { inventoryApi } from '../api';
import type { InventoryItem } from '../types';
import { RecordMovementDialog } from './RecordMovementDialog';

vi.mock('../../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../../catalog/api', () => ({ catalogApi: { listItems: vi.fn(), listCategories: vi.fn() } }));
vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: { recordMovement: vi.fn(), listInventoryItems: vi.fn(), listInventoryCategories: vi.fn() },
}));

const branches = [
  { id: 'kat', name: 'Katipunan', address: null },
  { id: 'kam', name: 'Kamuning', address: null },
];

const onClose = vi.fn();

describe('RecordMovementDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Warehouse', { scope_type: 'Tenant' });
    vi.mocked(catalogApi.listItems).mockResolvedValue([{ id: 'latte', name: 'Latte', isActive: true, categoryId: 'drinks' }] as Item[]);
    vi.mocked(catalogApi.listCategories).mockResolvedValue([{ id: 'drinks', name: 'Drinks', sortOrder: 0, imageUrl: null }]);
    vi.mocked(inventoryApi.listInventoryItems).mockResolvedValue([
      { id: 'beans', name: 'Coffee Beans', sku: null, baseUnit: 'g', isActive: true, isAutoCreatedForItem: false, categoryId: 'dry' },
      { id: 'latte-stock', name: 'Latte', sku: null, baseUnit: 'pc', isActive: true, isAutoCreatedForItem: true, categoryId: null },
    ] as InventoryItem[]);
    vi.mocked(inventoryApi.listInventoryCategories).mockResolvedValue([{ id: 'dry', name: 'Dry goods', sortOrder: 0 }]);
    vi.mocked(branchesApi.list).mockResolvedValue(branches);
    vi.mocked(inventoryApi.recordMovement).mockResolvedValue({} as never);
  });

  it('preselects the item and type it was opened with', async () => {
    renderPage(<RecordMovementDialog prefill={{ stockRef: 'item:latte', type: 1 }} onClose={onClose} />);
    expect(await screen.findByRole('combobox', { name: 'Item or ingredient' })).toHaveValue('Latte');
    expect(screen.getByLabelText('Movement type')).toHaveValue('1');
  });

  it('groups items and ingredients under their categories and hides the copy that only mirrors an item', async () => {
    renderPage(<RecordMovementDialog prefill={{}} onClose={onClose} />);
    fireEvent.click(await screen.findByRole('combobox', { name: 'Item or ingredient' }));
    expect(await screen.findByText('Items · Drinks')).toBeInTheDocument();
    expect(screen.getByText('Ingredients · Dry goods')).toBeInTheDocument();
    expect(within(screen.getByRole('listbox')).getAllByRole('option').map((o) => o.textContent)).toEqual(['Latte', 'Coffee Beansg']);
  });

  it('sends a stock-in for an item with the chosen branch', async () => {
    renderPage(<RecordMovementDialog prefill={{ stockRef: 'item:latte' }} onClose={onClose} />);
    fireEvent.change(await screen.findByLabelText('Branch'), { target: { value: 'kam' } });
    fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '12' } });
    fireEvent.click(screen.getByRole('button', { name: 'Record movement' }));

    await waitFor(() => expect(inventoryApi.recordMovement).toHaveBeenCalledTimes(1));
    expect(inventoryApi.recordMovement).toHaveBeenCalledWith({
      itemId: 'latte',
      inventoryItemId: null,
      branchId: 'kam',
      type: 0,
      quantity: 12,
      note: null,
      reasonCategory: null,
      photoUrl: null,
      supplierReference: null,
    });
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('records a consumption against an ingredient picked by typing', async () => {
    renderPage(<RecordMovementDialog prefill={{}} onClose={onClose} />);
    const picker = await screen.findByRole('combobox', { name: 'Item or ingredient' });
    fireEvent.change(picker, { target: { value: 'bean' } });
    fireEvent.click(await screen.findByRole('option', { name: /Coffee Beans/ }));
    fireEvent.change(screen.getByLabelText('Branch'), { target: { value: 'kat' } });
    fireEvent.change(screen.getByLabelText('Movement type'), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '30' } });
    fireEvent.click(screen.getByRole('button', { name: 'Record movement' }));

    await waitFor(() => expect(inventoryApi.recordMovement).toHaveBeenCalledTimes(1));
    expect(inventoryApi.recordMovement).toHaveBeenCalledWith(expect.objectContaining({ itemId: null, inventoryItemId: 'beans', type: 2, quantity: 30 }));
  });

  it('asks for a reason when the movement is spoilage and blocks the send', async () => {
    renderPage(<RecordMovementDialog prefill={{ stockRef: 'item:latte', type: 3 }} onClose={onClose} />);
    fireEvent.change(await screen.findByLabelText('Branch'), { target: { value: 'kat' } });
    fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Record movement' }));
    expect(await screen.findByText('Say why it spoiled')).toBeInTheDocument();
    expect(inventoryApi.recordMovement).not.toHaveBeenCalled();
  });

  it('never offers Sale as a type to record by hand', async () => {
    renderPage(<RecordMovementDialog prefill={{}} onClose={onClose} />);
    const select = await screen.findByLabelText('Movement type');
    expect([...select.querySelectorAll('option')].map((o) => o.textContent)).not.toContain('Sale');
  });

  it('gives a branch-scoped account only its own branch, already chosen', async () => {
    signInAs('Manager', { scope_type: 'Branch', scope_id: 'kam' });
    renderPage(<RecordMovementDialog prefill={{}} onClose={onClose} />);
    const branch = await screen.findByLabelText('Branch');
    expect(branch).toHaveValue('kam');
    expect([...branch.querySelectorAll('option')].map((o) => o.textContent)).toEqual(['Choose a branch', 'Kamuning']);
  });
});
