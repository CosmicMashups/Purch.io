import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as download from '../../../lib/download';
import { renderPage, signInAs } from '../../../test/render';
import { reportsApi } from '../api';
import type { ProfitReport, SalesMix } from '../revenueTypes';
import { ProfitPanel, SalesMixPanel } from './SalesPanels';

vi.mock('../api', () => ({ reportsApi: { salesMix: vi.fn(), grossProfit: vi.fn(), tax: vi.fn() } }));
vi.mock('../../../lib/download', async (importActual) => ({
  ...(await importActual<typeof import('../../../lib/download')>()),
  downloadTextFile: vi.fn(),
}));

const params = { from: '2026-10-01T00:00:00Z', to: '2026-10-31T00:00:00Z' };

const mix: SalesMix = {
  grossSales: 150,
  transactionCount: 2,
  operatingDays: 1,
  payments: [
    { method: 0, amount: 100, paymentCount: 1, isCredit: false },
    { method: 5, amount: 50, paymentCount: 1, isCredit: true },
  ],
  exchangeSettlements: 0,
  categories: [],
  hours: Array.from({ length: 24 }, (_, hour) => ({ hour, sales: hour === 9 ? 150 : 0, transactionCount: hour === 9 ? 2 : 0, averageBasket: 0, averagePerOperatingDay: 0 })),
  heatmap: [],
};

const profit: ProfitReport = {
  revenueExVat: 200,
  knownCostRevenue: 100,
  unknownCostRevenue: 100,
  cost: 60,
  grossProfit: 40,
  marginPercent: 40,
  coveragePercent: 50,
  days: [],
  categories: [],
  topItems: [],
  bottomItems: [],
};

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Admin');
});

describe('report export', () => {
  it('exports the payment mix as a CSV that matches the figures on screen', async () => {
    vi.mocked(reportsApi.salesMix).mockResolvedValue(mix);
    renderPage(<SalesMixPanel params={params} />);
    await screen.findByText('Utang (credit)');

    fireEvent.click(screen.getByRole('button', { name: 'Export CSV' }));

    await waitFor(() => expect(download.downloadTextFile).toHaveBeenCalled());
    const [filename, csv] = vi.mocked(download.downloadTextFile).mock.calls[0];
    expect(filename).toMatch(/^sales-by-payment-\d{4}-\d{2}-\d{2}\.csv$/);
    expect(csv).toBe('Payment type,Amount,Payments,Credit\nCash,100,1,No\nUtang (credit),50,1,Yes');
  });

  it('says how much of the profit report had no recorded cost and exports the same totals', async () => {
    vi.mocked(reportsApi.grossProfit).mockResolvedValue(profit);
    renderPage(<ProfitPanel params={params} />);

    expect(await screen.findByText(/Margin only covers items that have a cost price/)).toBeInTheDocument();
    expect(screen.getByText('50.0%', { selector: 'p' })).toBeInTheDocument();
  });
});
