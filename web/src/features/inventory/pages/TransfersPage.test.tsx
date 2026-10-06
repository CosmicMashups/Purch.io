import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { branchesApi } from '../../branches/api';
import { catalogApi } from '../../catalog/api';
import type { Item } from '../../catalog/types';
import { inventoryApi } from '../api';
import { BranchTransferStatus, type BranchTransfer, type InventoryItem } from '../types';
import { TransfersPage } from './TransfersPage';

vi.mock('../../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../../catalog/api', () => ({ catalogApi: { listItems: vi.fn(), listCategories: vi.fn() } }));
vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: {
    listTransfers: vi.fn(),
    listInventoryItems: vi.fn(),
    listInventoryCategories: vi.fn(),
    createTransfer: vi.fn(),
    markTransferInTransit: vi.fn(),
    markTransferReceived: vi.fn(),
    cancelTransfer: vi.fn(),
  },
}));

const transfer = (status: BranchTransferStatus): BranchTransfer => ({
  id: 't1',
  sourceBranchId: 'kat',
  sourceBranchName: 'Katipunan',
  destinationBranchId: 'kam',
  destinationBranchName: 'Kamuning',
  status,
  lines: [{ id: 'l', itemId: 'milk', itemName: 'Milk', quantity: 6 }],
});

describe('TransfersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Manager', { scope_type: 'Tenant' });
    vi.mocked(catalogApi.listItems).mockResolvedValue([{ id: 'milk', name: 'Milk', isActive: true, categoryId: 'dairy' }] as Item[]);
    vi.mocked(catalogApi.listCategories).mockResolvedValue([{ id: 'dairy', name: 'Dairy', sortOrder: 0, imageUrl: null }]);
    vi.mocked(inventoryApi.listInventoryItems).mockResolvedValue([
      { id: 'flour', name: 'Flour', sku: null, baseUnit: 'g', isActive: true, isAutoCreatedForItem: false, categoryId: 'dry' },
    ] as InventoryItem[]);
    vi.mocked(inventoryApi.listInventoryCategories).mockResolvedValue([{ id: 'dry', name: 'Dry goods', sortOrder: 0 }]);
    vi.mocked(branchesApi.list).mockResolvedValue([
      { id: 'kat', name: 'Katipunan', address: null },
      { id: 'kam', name: 'Kamuning', address: null },
    ]);
    vi.mocked(inventoryApi.listTransfers).mockResolvedValue([transfer(BranchTransferStatus.Pending)]);
  });

  it('sends a pending transfer only after confirming', async () => {
    vi.mocked(inventoryApi.markTransferInTransit).mockResolvedValue({} as never);
    renderPage(<TransfersPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Send' }));

    const dialog = screen.getByRole('dialog', { name: 'Send this transfer?' });
    expect(inventoryApi.markTransferInTransit).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Send transfer' }));
    await waitFor(() => expect(inventoryApi.markTransferInTransit).toHaveBeenCalledWith('t1'));
  });

  it('receives an in-transit transfer', async () => {
    vi.mocked(inventoryApi.listTransfers).mockResolvedValue([transfer(BranchTransferStatus.InTransit)]);
    vi.mocked(inventoryApi.markTransferReceived).mockResolvedValue({} as never);
    renderPage(<TransfersPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Mark received' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Mark received' }));
    await waitFor(() => expect(inventoryApi.markTransferReceived).toHaveBeenCalledWith('t1'));
  });

  it('treats cancelling as destructive and offers no actions once finished', async () => {
    vi.mocked(inventoryApi.listTransfers).mockResolvedValue([transfer(BranchTransferStatus.Pending), { ...transfer(BranchTransferStatus.Received), id: 't2' }]);
    renderPage(<TransfersPage />);
    await screen.findAllByText('Katipunan to Kamuning');
    expect(screen.getAllByRole('button', { name: 'Cancel' })).toHaveLength(1);
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.getByRole('dialog', { name: 'Cancel this transfer?' })).toBeInTheDocument();
  });

  async function openNewTransfer() {
    await screen.findByText('Katipunan to Kamuning');
    fireEvent.click(screen.getByRole('button', { name: 'New transfer' }));
    return screen.findByRole('dialog', { name: 'New transfer' });
  }

  it('keeps the form out of the page until New transfer is pressed', async () => {
    renderPage(<TransfersPage />);
    await screen.findByText('Katipunan to Kamuning');
    expect(screen.queryByLabelText('Send from')).not.toBeInTheDocument();
  });

  it('creates a transfer of an item between two different branches', async () => {
    vi.mocked(inventoryApi.createTransfer).mockResolvedValue({} as never);
    renderPage(<TransfersPage />);
    const dialog = await openNewTransfer();

    fireEvent.change(within(dialog).getByLabelText('Send from'), { target: { value: 'kat' } });
    fireEvent.change(within(dialog).getByLabelText('Send to'), { target: { value: 'kam' } });
    fireEvent.click(within(dialog).getByRole('combobox', { name: 'Item or ingredient' }));
    fireEvent.click(await screen.findByRole('option', { name: 'Milk' }));
    fireEvent.change(within(dialog).getByLabelText('Quantity'), { target: { value: '4' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Create transfer' }));

    await waitFor(() => expect(inventoryApi.createTransfer).toHaveBeenCalledTimes(1));
    expect(inventoryApi.createTransfer).toHaveBeenCalledWith({
      sourceBranchId: 'kat',
      destinationBranchId: 'kam',
      lines: [{ itemId: 'milk', inventoryItemId: null, quantity: 4 }],
    });
  });

  it('transfers an ingredient, found by typing, with the ingredient id', async () => {
    vi.mocked(inventoryApi.createTransfer).mockResolvedValue({} as never);
    renderPage(<TransfersPage />);
    const dialog = await openNewTransfer();

    fireEvent.change(within(dialog).getByLabelText('Send from'), { target: { value: 'kat' } });
    fireEvent.change(within(dialog).getByLabelText('Send to'), { target: { value: 'kam' } });
    const picker = within(dialog).getByRole('combobox', { name: 'Item or ingredient' });
    fireEvent.change(picker, { target: { value: 'flo' } });
    expect(screen.queryByRole('option', { name: 'Milk' })).not.toBeInTheDocument();
    fireEvent.click(await screen.findByRole('option', { name: /Flour/ }));
    fireEvent.change(within(dialog).getByLabelText('Quantity'), { target: { value: '500' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Create transfer' }));

    await waitFor(() => expect(inventoryApi.createTransfer).toHaveBeenCalledTimes(1));
    expect(inventoryApi.createTransfer).toHaveBeenCalledWith({
      sourceBranchId: 'kat',
      destinationBranchId: 'kam',
      lines: [{ itemId: null, inventoryItemId: 'flour', quantity: 500 }],
    });
  });

  it('blocks a transfer to the same branch', async () => {
    renderPage(<TransfersPage />);
    const dialog = await openNewTransfer();
    fireEvent.change(within(dialog).getByLabelText('Send from'), { target: { value: 'kat' } });
    fireEvent.change(within(dialog).getByLabelText('Send to'), { target: { value: 'kat' } });
    fireEvent.click(within(dialog).getByRole('combobox', { name: 'Item or ingredient' }));
    fireEvent.click(await screen.findByRole('option', { name: 'Milk' }));
    fireEvent.change(within(dialog).getByLabelText('Quantity'), { target: { value: '4' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Create transfer' }));
    expect(await screen.findByText('Choose a different branch to send to')).toBeInTheDocument();
    expect(inventoryApi.createTransfer).not.toHaveBeenCalled();
  });
});
