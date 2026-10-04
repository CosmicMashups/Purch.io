import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { inventoryApi } from '../../inventory/api';
import type { InventoryItem } from '../../inventory/types';
import { catalogApi } from '../api';
import type { Item } from '../types';
import { RecipePage } from './RecipePage';

vi.mock('../api', () => ({ catalogApi: { listItems: vi.fn(), getRecipe: vi.fn(), replaceRecipe: vi.fn() } }));
vi.mock('../../inventory/api', () => ({ inventoryApi: { listInventoryItems: vi.fn() } }));

const inv = (over: Partial<InventoryItem>): InventoryItem => ({
  id: 'x',
  name: 'X',
  sku: null,
  baseUnit: 'g',
  packagingUnit: 'kg',
  packagingSize: 1000,
  quantityOnHand: 0,
  lowStockThreshold: null,
  isAutoCreatedForItem: false,
  linkedItemId: null,
  isActive: true,
  categoryId: null,
  ...over,
});

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/catalog/items/latte/recipe']}>
        <Routes>
          <Route path="/catalog/items/:itemId/recipe" element={<RecipePage />} />
          <Route path="/catalog/items" element={<p>Item list</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('RecipePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(catalogApi.listItems).mockResolvedValue([{ id: 'latte', name: 'Latte' }] as Item[]);
    vi.mocked(inventoryApi.listInventoryItems).mockResolvedValue([
      inv({ id: 'beans', name: 'Espresso Beans' }),
      inv({ id: 'milk', name: 'Milk', baseUnit: 'ml', packagingUnit: 'L' }),
      inv({ id: 'own', name: 'Latte (own stock)', linkedItemId: 'latte' }),
    ]);
    vi.mocked(catalogApi.getRecipe).mockResolvedValue([{ inventoryItemId: 'beans', inventoryItemName: 'Espresso Beans', quantityPerOrder: 18 }]);
    vi.mocked(catalogApi.replaceRecipe).mockResolvedValue([]);
  });

  const pick = async (name: RegExp) => {
    fireEvent.focus(await screen.findByRole('combobox', { name: 'Ingredients' }));
    fireEvent.click(within(await screen.findByRole('listbox')).getByRole('option', { name }));
  };

  it("preloads the recipe and hides the item's own stock record", async () => {
    renderPage();
    const chosen = within(await screen.findByRole('list', { name: 'Selected ingredients' }));
    expect(chosen.getByText('Espresso Beans')).toBeInTheDocument();
    expect(chosen.getByRole('radio', { name: 'Used up every order' })).toBeChecked();
    expect(screen.getByLabelText('Quantity per order (g)')).toHaveValue('18');

    fireEvent.focus(screen.getByRole('combobox', { name: 'Ingredients' }));
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');
    expect(options).toHaveLength(2);
    expect(screen.queryByText('Latte (own stock)')).not.toBeInTheDocument();
  });

  it('saves a used ingredient with its quantity and an availability-only one without', async () => {
    renderPage();
    await screen.findByRole('list', { name: 'Selected ingredients' });
    await pick(/Milk/);
    // A newly chosen ingredient starts as availability-only.
    expect(screen.queryByLabelText('Quantity per order (ml)')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save recipe' }));

    await waitFor(() => expect(catalogApi.replaceRecipe).toHaveBeenCalledTimes(1));
    expect(catalogApi.replaceRecipe).toHaveBeenCalledWith('latte', {
      lines: [
        { inventoryItemId: 'beans', quantityPerOrder: 18 },
        { inventoryItemId: 'milk', quantityPerOrder: null },
      ],
    });
    expect(await screen.findByText('Item list')).toBeInTheDocument();
  });

  it('refuses a bad quantity without calling the API', async () => {
    renderPage();
    const qty = await screen.findByLabelText('Quantity per order (g)');
    fireEvent.change(qty, { target: { value: '-5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save recipe' }));
    expect(await screen.findByText('Cannot be negative')).toBeInTheDocument();
    expect(catalogApi.replaceRecipe).not.toHaveBeenCalled();
  });

  it('shows a retryable error when the recipe cannot load', async () => {
    vi.mocked(catalogApi.getRecipe).mockRejectedValue(new Error('boom'));
    renderPage();
    expect(await screen.findByText('The recipe could not be loaded')).toBeInTheDocument();
  });
});
