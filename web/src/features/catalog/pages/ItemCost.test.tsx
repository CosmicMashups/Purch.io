import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { inventoryApi } from '../../inventory/api';
import { tenantApi } from '../../tenant/api';
import type { TenantSettings } from '../../tenant/types';
import { catalogApi } from '../api';
import { PricingType, TingiMode, type Item } from '../types';
import { ItemDialog } from './ItemDialog';

vi.mock('../api', () => ({
  catalogApi: {
    listCategories: vi.fn(),
    listItems: vi.fn(),
    createItem: vi.fn(),
    updateItem: vi.fn(),
    updateLowStockThreshold: vi.fn(),
    getRecipe: vi.fn(),
    replaceRecipe: vi.fn(),
  },
}));
vi.mock('../../inventory/api', () => ({ MOVEMENT_PAGE_SIZE: 30, inventoryApi: { listInventoryItems: vi.fn() } }));
vi.mock('../../tenant/api', () => ({ tenantApi: { get: vi.fn() } }));

const latte: Item = {
  id: 'latte',
  sortOrder: 0,
  name: 'Latte',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 120,
  imageUrl: null,
  pricingType: PricingType.Unit,
  stockOnHand: 0,
  isActive: true,
  departmentId: null,
  tingiMode: TingiMode.None,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  lowStockThreshold: null,
  isOutOfStock: false,
  costPrice: 45,
};

const onClose = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Manager');
  vi.mocked(tenantApi.get).mockResolvedValue({ useSeparateInventoryTracking: false } as TenantSettings);
  vi.mocked(catalogApi.listCategories).mockResolvedValue([]);
  vi.mocked(inventoryApi.listInventoryItems).mockResolvedValue([]);
  vi.mocked(catalogApi.updateItem).mockResolvedValue(latte);
  vi.mocked(catalogApi.createItem).mockResolvedValue(latte);
});

describe('item cost price', () => {
  it('shows the saved cost with a margin hint', async () => {
    renderPage(<ItemDialog item={latte} onClose={onClose} />);
    expect(await screen.findByLabelText('Cost price (optional)')).toHaveValue('45');
    expect(screen.getByTestId('margin-hint')).toHaveTextContent('Margin ₱75.00 (63%)');
  });

  it('says cost unknown on a new item with no cost', async () => {
    renderPage(<ItemDialog item={null} onClose={onClose} />);
    expect(await screen.findByTestId('margin-hint')).toHaveTextContent('Cost unknown');
  });

  it('clears a saved cost by sending clearCostPrice', async () => {
    renderPage(<ItemDialog item={latte} onClose={onClose} />);
    fireEvent.click(await screen.findByRole('button', { name: 'Clear cost' }));
    expect(screen.getByTestId('margin-hint')).toHaveTextContent('Cost unknown');
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(catalogApi.updateItem).toHaveBeenCalled());
    const body = vi.mocked(catalogApi.updateItem).mock.calls[0][1];
    expect(body.clearCostPrice).toBe(true);
    expect(body.costPrice).toBeUndefined();
  });

  it('sends a changed cost and refuses a negative one', async () => {
    renderPage(<ItemDialog item={latte} onClose={onClose} />);
    const field = await screen.findByLabelText('Cost price (optional)');
    fireEvent.change(field, { target: { value: '-3' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
    expect(await screen.findByText('Cost cannot be negative')).toBeInTheDocument();
    expect(catalogApi.updateItem).not.toHaveBeenCalled();
    fireEvent.change(field, { target: { value: '50.5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(catalogApi.updateItem).toHaveBeenCalled());
    expect(vi.mocked(catalogApi.updateItem).mock.calls[0][1].costPrice).toBe(50.5);
  });

  it('never shows cost or margin to a Cashier', async () => {
    signInAs('Cashier');
    renderPage(<ItemDialog item={{ ...latte, costPrice: null }} onClose={onClose} />);
    await screen.findByLabelText('Name');
    expect(screen.queryByLabelText('Cost price (optional)')).not.toBeInTheDocument();
    expect(screen.queryByTestId('margin-hint')).not.toBeInTheDocument();
  });
});
