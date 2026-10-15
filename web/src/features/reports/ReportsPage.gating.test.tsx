import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { branchesApi } from '../branches/api';
import { tenantApi } from '../tenant/api';
import { ReportsPage } from './ReportsPage';

vi.mock('./api', () => ({ reportsApi: new Proxy({}, { get: () => vi.fn().mockResolvedValue({ groups: [], hours: [], payments: [], categories: [], heatmap: [] }) }) }));
vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../tenant/api', () => ({ tenantApi: { get: vi.fn() } }));

beforeEach(() => {
  vi.mocked(branchesApi.list).mockResolvedValue([]);
  vi.mocked(tenantApi.get).mockResolvedValue({} as never);
});

describe('stock report tabs', () => {
  it('are offered to a Manager', async () => {
    signInAs('Manager');
    renderPage(<ReportsPage />, { route: '/reports?tab=valuation' });
    expect(await screen.findByRole('tab', { name: 'Stock valuation' })).toBeInTheDocument();
  });

  it('are hidden from a Cashier, and a deep link falls back to the default tab', async () => {
    signInAs('Cashier');
    renderPage(<ReportsPage />, { route: '/reports?tab=valuation' });
    expect(await screen.findByRole('heading', { name: 'Reports' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Stock valuation' })).toBeNull();
    expect(screen.queryByRole('tab', { name: 'COGS' })).toBeNull();
    expect(screen.queryByText('Stock valuation')).toBeNull();
  });
});
