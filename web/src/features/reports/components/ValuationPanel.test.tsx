import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as download from '../../../lib/download';
import { renderPage, signInAs } from '../../../test/render';
import { reportsApi } from '../api';
import type { InventoryValuation } from '../types';
import { ValuationPanel } from './ValuationPanel';

vi.mock('../api', () => ({ reportsApi: { inventoryValuation: vi.fn(), inventoryCogs: vi.fn() } }));
vi.mock('../../../lib/download', async (importActual) => ({
  ...(await importActual<typeof import('../../../lib/download')>()),
  downloadTextFile: vi.fn(),
}));

const row = (over: object) => ({ id: 'i', kind: 'item', name: 'Rice', sku: null, category: 'Dry', onHand: 2, unitCost: 10, value: 20, flagged: false, ...over });
const data: InventoryValuation = {
  groups: [
    {
      categoryId: null,
      name: 'Dry',
      subtotal: 20,
      rows: [row({}), row({ id: 'u', name: '=Mystery', unitCost: null, value: null, flagged: true }), row({ id: 'n', name: 'Neg', onHand: -1, value: 0, flagged: true })],
    },
  ],
  totalValue: 20,
  valuedCount: 2,
  unknownCostCount: 1,
  negativeStockCount: 1,
};

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Admin');
  vi.mocked(reportsApi.inventoryValuation).mockResolvedValue(data);
});

describe('ValuationPanel', () => {
  it('shows the unknown-cost banner with the count and the negative-stock note', async () => {
    renderPage(<ValuationPanel />);
    expect(await screen.findByText(/Unknown cost excluded from the total/)).toBeInTheDocument();
    expect(screen.getByText(/1 item has no cost/)).toBeInTheDocument();
    expect(screen.getByText(/1 item has negative stock, valued at 0/)).toBeInTheDocument();
    expect(screen.getByText('₱20.00', { selector: 'p' })).toBeInTheDocument();
  });

  it('exports a CSV with a header and defused formula cells', async () => {
    renderPage(<ValuationPanel />);
    await screen.findByText(/Unknown cost excluded/);
    fireEvent.click(screen.getByRole('button', { name: 'Export CSV' }));
    await waitFor(() => expect(download.downloadTextFile).toHaveBeenCalled());
    const csv = vi.mocked(download.downloadTextFile).mock.calls[0][1] as string;
    expect(csv.split('\n')[0]).toBe('Category,Name,Kind,SKU,On hand,Unit cost,Value,Flag');
    expect(csv).toContain("'=Mystery");
  });
});
