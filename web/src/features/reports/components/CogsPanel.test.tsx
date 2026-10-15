import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { reportsApi } from '../api';
import { CogsPanel } from './CogsPanel';

vi.mock('../api', () => ({ reportsApi: { inventoryCogs: vi.fn() } }));
const params = { from: '2026-10-01T00:00:00Z', to: '2026-10-31T00:00:00Z' };

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Admin');
  vi.mocked(reportsApi.inventoryCogs).mockResolvedValue({
    groups: [{ id: null, name: 'Rice', quantity: 3, cogs: 30, knownCostRevenue: 60, coveragePercent: 80, excludedLineCount: 2 }],
    cogs: 30, knownCostRevenue: 60, coveragePercent: 80, excludedLineCount: 2,
  });
});

describe('CogsPanel', () => {
  it('shows coverage, excluded lines and the recipe caveat', async () => {
    renderPage(<CogsPanel params={params} />);
    expect(await screen.findByText(/Recipe use is not included in COGS/)).toBeInTheDocument();
    expect(screen.getByText(/refunds net against the original sale date/)).toBeInTheDocument();
    expect(screen.getByText('80.0%', { selector: 'p' })).toBeInTheDocument();
    expect(screen.getByText(/2 lines have no recorded cost/)).toBeInTheDocument();
  });
});
