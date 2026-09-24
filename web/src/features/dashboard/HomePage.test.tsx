import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { reportsApi } from '../reports/api';
import { dashboardApi } from './api';
import { HomePage } from './HomePage';
import type { InventoryDashboard, SalesDashboard } from './types';

vi.mock('../reports/api', () => ({ reportsApi: { staffPerformance: vi.fn(), departmentSales: vi.fn(), movementSummary: vi.fn() } }));
vi.mock('./api', () => ({
  dashboardApi: { sales: vi.fn(), inventory: vi.fn(), flaggedSync: vi.fn() },
}));

function tokenFor(role: string): string {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, '');
  return `${b64({ alg: 'none' })}.${b64({ role, sub: 's', tenant_id: 't' })}.x`;
}

function renderHome(role: string) {
  useAuthStore.setState({ accessToken: tokenFor(role), refreshToken: 'r' });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const sales: SalesDashboard = {
  revenueToday: 1250.5,
  revenueLast7Days: 8000,
  revenueLast30Days: 31000,
  trend: [
    { date: '2026-09-23', revenue: 900 },
    { date: '2026-09-24', revenue: 1250.5 },
  ],
  topSellingItems: [{ itemId: 'i1', itemName: 'Iced Latte', quantitySold: 42, revenue: 6300 }],
  branchComparison: [{ branchId: 'b1', branchName: 'Main', revenue: 31000 }],
};

const inventory: InventoryDashboard = {
  totalSkus: 10,
  outOfStockCount: 1,
  lowStockCount: 1,
  lowStockItems: [{ itemId: 'i2', itemName: 'Oat Milk', stockOnHand: 3, lowStockThreshold: 12 }],
};

describe('HomePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(dashboardApi.sales).mockResolvedValue(sales);
    vi.mocked(dashboardApi.inventory).mockResolvedValue(inventory);
    vi.mocked(dashboardApi.flaggedSync).mockResolvedValue([]);
    vi.mocked(reportsApi.staffPerformance).mockResolvedValue({ sales: [], shiftAttendance: [] });
    vi.mocked(reportsApi.departmentSales).mockResolvedValue([]);
    vi.mocked(reportsApi.movementSummary).mockResolvedValue({ from: '', to: '', byType: [] });
  });

  it('shows revenue, top sellers and low stock to an Admin', async () => {
    renderHome('Admin');
    // The amount also appears in the trend chart's accessible table, so expect at least the card.
    expect((await screen.findAllByText('₱1,250.50')).length).toBeGreaterThan(0);
    expect(await screen.findByText('Iced Latte')).toBeInTheDocument();
    expect(await screen.findByText('Oat Milk')).toBeInTheDocument();
  });

  it('does not call reporting or inventory endpoints for a Cashier', async () => {
    renderHome('Cashier');
    expect(await screen.findByRole('link', { name: 'New sale' })).toBeInTheDocument();
    expect(dashboardApi.sales).not.toHaveBeenCalled();
    expect(dashboardApi.inventory).not.toHaveBeenCalled();
    expect(dashboardApi.flaggedSync).not.toHaveBeenCalled();
    expect(reportsApi.staffPerformance).not.toHaveBeenCalled();
    expect(reportsApi.departmentSales).not.toHaveBeenCalled();
    expect(reportsApi.movementSummary).not.toHaveBeenCalled();
  });

  it('shows stock but no revenue to a Warehouse user', async () => {
    renderHome('Warehouse');
    expect(await screen.findByText('Oat Milk')).toBeInTheDocument();
    expect(dashboardApi.sales).not.toHaveBeenCalled();
    expect(screen.queryByText('Last 7 days')).not.toBeInTheDocument();
  });

  it('flags unreviewed sync conflicts', async () => {
    vi.mocked(dashboardApi.flaggedSync).mockResolvedValue([
      { id: 'a', deviceId: 'd', entityType: 'Sale', entityId: 'e', clientTimestamp: '2026-09-24T00:00:00Z', reviewedAt: null },
      { id: 'b', deviceId: 'd', entityType: 'Sale', entityId: 'f', clientTimestamp: '2026-09-24T00:00:00Z', reviewedAt: '2026-09-24T01:00:00Z' },
    ]);
    renderHome('Manager');
    expect(await screen.findByText('1 offline record needs review')).toBeInTheDocument();
  });

  it('shows a friendly retryable error instead of raw server text', async () => {
    vi.mocked(dashboardApi.sales).mockRejectedValue(new ApiError('unknown', 'System.InvalidOperationException stack'));
    renderHome('Admin');
    await waitFor(() => expect(screen.getAllByText('Something went wrong. Please try again.').length).toBeGreaterThan(0));
    expect(screen.queryByText(/InvalidOperation/)).not.toBeInTheDocument();
  });
});
