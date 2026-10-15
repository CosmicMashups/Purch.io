import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
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
});
