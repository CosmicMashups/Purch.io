import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { catalogApi } from '../api';
import type { ModifierGroup } from '../types';
import { ModifierGroupsPage } from './ModifierGroupsPage';

vi.mock('../api', () => ({
  catalogApi: {
    listModifierGroups: vi.fn(),
    listCategories: vi.fn(),
    createModifierGroup: vi.fn(),
    updateModifierGroup: vi.fn(),
    updateModifierCategoryItem: vi.fn(),
  },
}));
vi.mock('../../tenant/queries', () => ({ useTenantSettings: () => ({ data: { useSeparateInventoryTracking: false } }) }));

const group: ModifierGroup = {
  id: 'g1',
  name: 'Add fries & sides',
  allowMultipleSelection: true,
  isRequired: false,
  modifiers: [],
  categoryId: 'cat1',
  categoryItems: [{ itemId: 'fries', name: 'Large Fries', imageUrl: null, basePrice: 80, priceOverride: null, price: 80, isExcluded: false, isOutOfStock: false }],
};

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ModifierGroupsPage />
    </QueryClientProvider>,
  );
}

describe('ModifierGroupsPage category link', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(catalogApi.listModifierGroups).mockResolvedValue([group]);
    vi.mocked(catalogApi.listCategories).mockResolvedValue([{ id: 'cat1', name: 'Fries & sides', sortOrder: 1, imageUrl: null }]);
  });

  it('says which category the group also offers', async () => {
    renderPage();
    expect(await screen.findByText('Also offers every item in Fries & sides')).toBeInTheDocument();
  });

  it('creates a group linked to a category', async () => {
    vi.mocked(catalogApi.createModifierGroup).mockResolvedValue(group);
    renderPage();
    await screen.findByText('Add fries & sides');
    fireEvent.click(screen.getByRole('button', { name: 'Add group' }));
    fireEvent.change(screen.getByLabelText('Group name'), { target: { value: 'More sides' } });
    fireEvent.change(await screen.findByLabelText('Offer items from a category (optional)'), { target: { value: 'cat1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add Group' }));
    await waitFor(() => expect(catalogApi.createModifierGroup).toHaveBeenCalledTimes(1));
    expect(catalogApi.createModifierGroup).toHaveBeenCalledWith({ name: 'More sides', allowMultipleSelection: false, isRequired: false, categoryId: 'cat1' });
  });

  it('sets a price for one category item in the group', async () => {
    vi.mocked(catalogApi.updateModifierCategoryItem).mockResolvedValue(group);
    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Category of Add fries & sides' }));
    fireEvent.change(await screen.findByLabelText('Price of Large Fries in this group'), { target: { value: '60' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Large Fries' }));
    await waitFor(() => expect(catalogApi.updateModifierCategoryItem).toHaveBeenCalledWith('g1', 'fries', { priceOverride: 60, isExcluded: false }));
  });

  it('hides a category item from the group', async () => {
    vi.mocked(catalogApi.updateModifierCategoryItem).mockResolvedValue(group);
    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Category of Add fries & sides' }));
    fireEvent.click(await screen.findByLabelText('Hide Large Fries from this group'));
    fireEvent.click(screen.getByRole('button', { name: 'Save Large Fries' }));
    await waitFor(() => expect(catalogApi.updateModifierCategoryItem).toHaveBeenCalledWith('g1', 'fries', { priceOverride: null, isExcluded: true }));
  });

  it('unlinks the category', async () => {
    vi.mocked(catalogApi.updateModifierGroup).mockResolvedValue({ ...group, categoryId: null, categoryItems: null });
    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Category of Add fries & sides' }));
    fireEvent.change(await screen.findByLabelText('Category offered in Add fries & sides'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save category' }));
    await waitFor(() => expect(catalogApi.updateModifierGroup).toHaveBeenCalledWith('g1', { name: 'Add fries & sides', allowMultipleSelection: true, isRequired: false, categoryId: null }));
  });
});
