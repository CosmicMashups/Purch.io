import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { catalogApi } from '../../catalog/api';
import { renderPage, signInAs } from '../../../test/render';
import { inventoryApi } from '../api';
import { StockTakeScopeKind, StockTakeStatus } from '../types';
import { StockTakesPage } from './StockTakesPage';

vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: {
    stockTakes: { list: vi.fn(), create: vi.fn() },
    listInventoryItems: vi.fn().mockResolvedValue([]),
    listInventoryCategories: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock('../../catalog/api', () => ({
  catalogApi: {
    listItems: vi.fn().mockResolvedValue([{ id: 'milk', name: 'Milk', isActive: true, categoryId: 'dairy' }]),
    listCategories: vi.fn().mockResolvedValue([{ id: 'dairy', name: 'Dairy', sortOrder: 0, imageUrl: null }]),
  },
}));

describe('StockTakesPage', () => {
  beforeEach(() => signInAs('Manager', { scope_type: 'Tenant' }));

  it('lists takes with status and progress', async () => {
    vi.mocked(inventoryApi.stockTakes.list).mockResolvedValue([
      { id: 't1', name: 'Monthly', status: StockTakeStatus.Counting, blind: true, scopeKind: StockTakeScopeKind.All, createdAt: '2026-10-01', appliedAt: null, lineCount: 4, countedCount: 1 },
    ]);
    renderPage(<StockTakesPage />);
    expect(await screen.findByText('Monthly')).toBeInTheDocument();
    expect(screen.getByText(/1 of 4 counted/)).toBeInTheDocument();
    expect(screen.getByText('Blind')).toBeInTheDocument();
  });

  it('blocks an empty categories selection, then sends categoryIds', async () => {
    vi.mocked(inventoryApi.stockTakes.list).mockResolvedValue([]);
    vi.mocked(inventoryApi.stockTakes.create).mockResolvedValue({ id: 'n' } as never);
    renderPage(<StockTakesPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'New stock take' }));
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Dairy' } });
    fireEvent.change(screen.getByLabelText('What to count'), { target: { value: String(StockTakeScopeKind.Categories) } });
    fireEvent.click(screen.getByRole('button', { name: 'Create stock take' }));
    expect(await screen.findByText('Choose at least one category.')).toBeInTheDocument();
    expect(inventoryApi.stockTakes.create).not.toHaveBeenCalled();
    fireEvent.click(await screen.findByLabelText('Dairy'));
    fireEvent.click(screen.getByRole('button', { name: 'Create stock take' }));
    await waitFor(() => expect(inventoryApi.stockTakes.create).toHaveBeenCalledWith(expect.objectContaining({ categoryIds: ['dairy'] })));
  });

  it('sends itemIds for picked catalog items', async () => {
    vi.mocked(inventoryApi.stockTakes.list).mockResolvedValue([]);
    vi.mocked(inventoryApi.stockTakes.create).mockResolvedValue({ id: 'n' } as never);
    renderPage(<StockTakesPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'New stock take' }));
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Pick' } });
    fireEvent.change(screen.getByLabelText('What to count'), { target: { value: String(StockTakeScopeKind.Picked) } });
    fireEvent.click(await screen.findByLabelText('Milk'));
    fireEvent.click(screen.getByRole('button', { name: 'Create stock take' }));
    await waitFor(() =>
      expect(inventoryApi.stockTakes.create).toHaveBeenCalledWith(expect.objectContaining({ itemIds: ['milk'], inventoryItemIds: [] })),
    );
  });
});
