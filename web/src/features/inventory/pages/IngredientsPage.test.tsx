import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { branchesApi } from '../../branches/api';
import { inventoryApi } from '../api';
import type { InventoryItem } from '../types';
import { IngredientsPage } from './IngredientsPage';

vi.mock('../../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: {
    listInventoryItems: vi.fn(),
    createInventoryItem: vi.fn(),
    updateInventoryItem: vi.fn(),
    physicalCount: vi.fn(),
    receiveStock: vi.fn(),
  },
}));

const beans: InventoryItem = {
  id: 'beans',
  name: 'Espresso Beans',
  sku: 'EB-1',
  baseUnit: 'g',
  packagingUnit: 'sack',
  packagingSize: 1000,
  quantityOnHand: 2500,
  lowStockThreshold: 500,
  isAutoCreatedForItem: false,
  linkedItemId: null,
  isActive: true,
};

describe('IngredientsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Warehouse', { scope_type: 'Branch', scope_id: 'kat' });
    vi.mocked(branchesApi.list).mockResolvedValue([
      { id: 'kat', name: 'Katipunan', address: null },
      { id: 'kam', name: 'Kamuning', address: null },
    ]);
    vi.mocked(inventoryApi.listInventoryItems).mockResolvedValue([beans]);
  });

  it('shows stock and pack size in plain words', async () => {
    renderPage(<IngredientsPage />);
    expect(await screen.findByText('2500 g on hand')).toBeInTheDocument();
    expect(screen.getByText(/1 sack = 1000 g, alert at 500/)).toBeInTheDocument();
  });

  it('adds an ingredient, turning blank optional fields into null', async () => {
    vi.mocked(inventoryApi.createInventoryItem).mockResolvedValue(beans);
    renderPage(<IngredientsPage />);
    await screen.findByText('2500 g on hand');
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Oat Milk' } });
    fireEvent.change(screen.getByLabelText('Base unit'), { target: { value: 'ml' } });
    fireEvent.change(screen.getByLabelText('Packaging unit'), { target: { value: 'case' } });
    fireEvent.change(screen.getByLabelText('Packaging size'), { target: { value: '12000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));

    await waitFor(() => expect(inventoryApi.createInventoryItem).toHaveBeenCalledTimes(1));
    expect(inventoryApi.createInventoryItem).toHaveBeenCalledWith({
      name: 'Oat Milk',
      sku: null,
      baseUnit: 'ml',
      packagingUnit: 'case',
      packagingSize: 12000,
      lowStockThreshold: null,
    });
  });

  it('edits an existing ingredient with its values preloaded', async () => {
    vi.mocked(inventoryApi.updateInventoryItem).mockResolvedValue(beans);
    renderPage(<IngredientsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Edit' }));
    expect(screen.getByLabelText('Name')).toHaveValue('Espresso Beans');
    expect(screen.getByLabelText('Low stock alert (optional)')).toHaveValue('500');
    fireEvent.change(screen.getByLabelText('Low stock alert (optional)'), { target: { value: '750' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(inventoryApi.updateInventoryItem).toHaveBeenCalledTimes(1));
    expect(vi.mocked(inventoryApi.updateInventoryItem).mock.calls[0]).toEqual([
      'beans',
      { name: 'Espresso Beans', sku: 'EB-1', baseUnit: 'g', packagingUnit: 'sack', packagingSize: 1000, lowStockThreshold: 750, isActive: true },
    ]);
  });

  it('records a physical count for the account\'s only branch', async () => {
    vi.mocked(inventoryApi.physicalCount).mockResolvedValue(beans);
    renderPage(<IngredientsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Count stock' }));
    expect(await screen.findByText('Count stock: Espresso Beans')).toBeInTheDocument();
    expect(screen.getByLabelText('Branch')).toHaveValue('kat');
    fireEvent.change(screen.getByLabelText('Counted quantity (g)'), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save count' }));
    await waitFor(() => expect(inventoryApi.physicalCount).toHaveBeenCalledWith('beans', { quantityOnHand: 0, branchId: 'kat' }));
  });

  it('records a delivery in packages', async () => {
    vi.mocked(inventoryApi.receiveStock).mockResolvedValue(beans);
    renderPage(<IngredientsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Receive delivery' }));
    fireEvent.change(await screen.findByLabelText('Packages received (sack)'), { target: { value: '3' } });
    fireEvent.click(screen.getAllByRole('button', { name: 'Receive delivery' }).find((b) => b.getAttribute('type') === 'submit')!);
    await waitFor(() => expect(inventoryApi.receiveStock).toHaveBeenCalledWith('beans', { packagesReceived: 3, branchId: 'kat', supplierReference: null }));
  });

  it('rejects a negative alert level before calling the API', async () => {
    renderPage(<IngredientsPage />);
    await screen.findByText('2500 g on hand');
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'X' } });
    fireEvent.change(screen.getByLabelText('Base unit'), { target: { value: 'g' } });
    fireEvent.change(screen.getByLabelText('Packaging unit'), { target: { value: 'bag' } });
    fireEvent.change(screen.getByLabelText('Packaging size'), { target: { value: '5' } });
    fireEvent.change(screen.getByLabelText('Low stock alert (optional)'), { target: { value: '-3' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Cannot be negative')).toBeInTheDocument();
    expect(inventoryApi.createInventoryItem).not.toHaveBeenCalled();
  });
});
