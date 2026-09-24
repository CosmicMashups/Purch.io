import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuthStore } from '../../lib/authStore';
import { reportsApi } from '../reports/api';
import { dashboardApi } from './api';
import { HomePage } from './HomePage';
import type { InventoryDashboard, SalesDashboard } from './types';

vi.mock('../reports/api', () => ({ reportsApi: { staffPerformance: vi.fn(), departmentSales: vi.fn(), movementSummary: vi.fn() } }));
vi.mock('./api', () => ({ dashboardApi: { sales: vi.fn(), inventory: vi.fn(), flaggedSync: vi.fn() } }));

function renderHome(role = 'Admin') {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, '');
  useAuthStore.setState({ accessToken: `${b64({ alg: 'none' })}.${b64({ role, sub: 's', tenant_id: 't' })}.x`, refreshToken: 'r' });
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

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(dashboardApi.sales).mockResolvedValue(sales);
  vi.mocked(dashboardApi.inventory).mockResolvedValue(inventory);
  vi.mocked(dashboardApi.flaggedSync).mockResolvedValue([]);
  vi.mocked(reportsApi.staffPerformance).mockResolvedValue({ sales: [], shiftAttendance: [] });
  vi.mocked(reportsApi.departmentSales).mockResolvedValue([]);
  vi.mocked(reportsApi.movementSummary).mockResolvedValue({ from: '', to: '', byType: [] });
});

describe('Home charts', () => {
  it('leads with today, then the shape of the days and a calendar of the busiest', async () => {
    renderHome();
    expect(await screen.findByText('Today')).toBeInTheDocument();
    expect(screen.getAllByText('₱8,000.00').length).toBeGreaterThan(0);
    expect(await screen.findByText('Best: ₱1,250.50')).toBeInTheDocument();
    expect(screen.getByText('Busiest days')).toBeInTheDocument();
    expect(screen.getByText('Less')).toBeInTheDocument();
  });

  it('shows stock as counts against the whole range, in words as well as colour', async () => {
    renderHome();
    expect(await screen.findByRole('meter', { name: 'Running low' })).toHaveAttribute('aria-valuenow', '1');
    expect(screen.getByRole('meter', { name: 'Out of stock' })).toHaveAttribute('aria-valuemax', '10');
    expect(screen.getByText('1 item at or under the warning level')).toBeInTheDocument();
    expect(screen.getByText('1 item cannot be sold')).toBeInTheDocument();
    expect(await screen.findByRole('img', { name: 'Oat Milk: 3 left, alert at 12' })).toBeInTheDocument();
  });

  it('is good news, in words, when nothing is low', async () => {
    vi.mocked(dashboardApi.inventory).mockResolvedValue({ totalSkus: 10, outOfStockCount: 0, lowStockCount: 0, lowStockItems: [] });
    renderHome();
    expect(await screen.findByText('Nothing is running low right now.')).toBeInTheDocument();
    expect(screen.getByText('Everything can be sold')).toBeInTheDocument();
  });

  it('splits revenue by branch only when there is more than one', async () => {
    const { unmount } = renderHome();
    expect(await screen.findByText('Branch comparison shows once more than one branch is in your scope.')).toBeInTheDocument();
    unmount();
    vi.mocked(dashboardApi.sales).mockResolvedValue({
      ...sales,
      branchComparison: [
        { branchId: 'b1', branchName: 'Main', revenue: 3000 },
        { branchId: 'b2', branchName: 'Annex', revenue: 1000 },
      ],
    });
    renderHome();
    expect(await screen.findByText('₱3,000.00, 75%')).toBeInTheDocument();
    expect(screen.getByText('₱1,000.00, 25%')).toBeInTheDocument();
  });

  it('draws departments as a ring with a legend, and as bars when there are too few to split', async () => {
    vi.mocked(reportsApi.departmentSales).mockResolvedValue([
      { departmentId: 'd1', departmentName: 'Coffee bar', revenue: 6000 },
      { departmentId: 'd2', departmentName: 'Kitchen', revenue: 3000 },
      { departmentId: null, departmentName: 'General', revenue: 1000 },
    ]);
    const { unmount } = renderHome();
    expect(await screen.findByText('₱6,000.00, 60%')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Revenue by department' })).toBeInTheDocument();
    unmount();

    vi.mocked(reportsApi.departmentSales).mockResolvedValue([{ departmentId: 'd1', departmentName: 'Coffee bar', revenue: 6000 }]);
    renderHome();
    await waitFor(() => expect(screen.getByText('₱6,000.00')).toBeInTheDocument());
    expect(screen.queryByRole('img', { name: 'Revenue by department' })).not.toBeInTheDocument();
  });

  it('folds departments past six into one Other', async () => {
    vi.mocked(reportsApi.departmentSales).mockResolvedValue(Array.from({ length: 9 }, (_, i) => ({ departmentId: `d${i}`, departmentName: `Dept ${i}`, revenue: 1000 - i * 10 })));
    renderHome();
    expect(await screen.findByText('Other (3)')).toBeInTheDocument();
  });

  it('ranks cashiers, and shows shift cash differences as a meter per person', async () => {
    vi.mocked(reportsApi.staffPerformance).mockResolvedValue({
      sales: [
        { staffUserId: 'a', staffName: 'Ana Reyes', transactionCount: 30, totalSales: 9000 },
        { staffUserId: 'b', staffName: 'Ben Cruz', transactionCount: 1, totalSales: 200 },
      ],
      shiftAttendance: [
        { staffUserId: 'a', staffName: 'Ana Reyes', shiftsOpened: 5, shiftsWithVariance: 0 },
        { staffUserId: 'b', staffName: 'Ben Cruz', shiftsOpened: 2, shiftsWithVariance: 1 },
      ],
    });
    renderHome();
    expect(await screen.findByText('30 sales')).toBeInTheDocument();
    expect(screen.getByText('1 sale')).toBeInTheDocument();
    expect(await screen.findByText('Every cash count matched')).toBeInTheDocument();
    expect(screen.getByText('1 shift closed with a cash difference')).toBeInTheDocument();
  });

  it('shows a warehouse user their stock and nothing about money', async () => {
    renderHome('Warehouse');
    expect(await screen.findByRole('meter', { name: 'Running low' })).toBeInTheDocument();
    expect(screen.queryByText('Busiest days')).not.toBeInTheDocument();
    expect(screen.queryByText('Revenue')).not.toBeInTheDocument();
  });
});
