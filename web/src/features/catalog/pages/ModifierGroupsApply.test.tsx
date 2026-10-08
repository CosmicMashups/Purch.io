import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { chooseFromMenu } from '../../../test/menu';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { catalogApi } from '../api';
import type { Item, ModifierGroup } from '../types';
import { ModifierGroupsPage } from './ModifierGroupsPage';

vi.mock('../api', () => ({
  catalogApi: {
    listModifierGroups: vi.fn(),
    listCategories: vi.fn(),
    listItems: vi.fn(),
    attachModifierGroupToItems: vi.fn(),
  },
}));
vi.mock('../../tenant/queries', () => ({ useTenantSettings: () => ({ data: { useSeparateInventoryTracking: false } }) }));

const group: ModifierGroup = { id: 'g1', name: 'Sweetness', allowMultipleSelection: false, isRequired: true, modifiers: [], categoryId: null, categoryItems: null };
const item = (id: string, name: string, categoryId: string | null) => ({ id, name, categoryId, isActive: true, basePrice: 50 }) as Item;

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ModifierGroupsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ModifierGroupsPage apply to items', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(catalogApi.listModifierGroups).mockResolvedValue([group]);
    vi.mocked(catalogApi.listCategories).mockResolvedValue([{ id: 'drinks', name: 'Drinks', sortOrder: 0, imageUrl: null }]);
    vi.mocked(catalogApi.listItems).mockResolvedValue([item('tea', 'Tea', 'drinks'), item('cake', 'Cake', null)]);
    vi.mocked(catalogApi.attachModifierGroupToItems).mockResolvedValue({ attached: 1, alreadyAttached: 0 });
  });

  it('gives the group to every item of a category', async () => {
    renderPage();
    await chooseFromMenu('Sweetness', 'Apply to items');
    fireEvent.change(await screen.findByLabelText('Category to give Sweetness to'), { target: { value: 'drinks' } });
    fireEvent.click(screen.getByRole('button', { name: 'Apply' }));
    await waitFor(() => expect(catalogApi.attachModifierGroupToItems).toHaveBeenCalledWith('g1', { categoryId: 'drinks' }));
  });

  it('gives the group to the items ticked', async () => {
    renderPage();
    await chooseFromMenu('Sweetness', 'Apply to items');
    fireEvent.click(screen.getByLabelText('Selected items'));
    fireEvent.click(await screen.findByLabelText('Cake'));
    fireEvent.click(screen.getByRole('button', { name: 'Apply' }));
    await waitFor(() => expect(catalogApi.attachModifierGroupToItems).toHaveBeenCalledWith('g1', { itemIds: ['cake'] }));
  });
});
