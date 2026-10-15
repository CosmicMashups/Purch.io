import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { toast } from '../../../components/feedback/toastStore';
import { ApiError } from '../../../lib/apiError';
import { renderPage, signInAs } from '../../../test/render';
import { inventoryApi } from '../api';
import { StockTakeLineState, StockTakeScopeKind, StockTakeStatus, type StockTake, type StockTakeLine } from '../types';
import { StockTakeDetailPage } from './StockTakeDetailPage';

vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: {
    stockTakes: { get: vi.fn(), countLine: vi.fn(), start: vi.fn(), review: vi.fn(), apply: vi.fn(), cancel: vi.fn() },
  },
}));

const line = (over: Partial<StockTakeLine>): StockTakeLine => ({
  id: 'l1', itemId: null, inventoryItemId: 'i1', name: 'Flour', categoryId: null, state: StockTakeLineState.Uncounted,
  expectedQty: 10, countedQty: null, varianceQty: null, unitCostSnapshot: 2, varianceValue: null, ...over,
});
const take = (over: Partial<StockTake>): StockTake => ({
  id: 't1', name: 'Monthly', status: StockTakeStatus.Counting, blind: false, scopeKind: StockTakeScopeKind.All, branchId: 'b',
  createdAt: '2026-10-01', startedAt: null, appliedAt: null, note: null, countedCount: 0, uncountedCount: 1, skippedCount: 0,
  lines: [line({})], warnings: [], ...over,
});

function show(role: string, data: StockTake) {
  signInAs(role, { scope_type: 'Tenant' });
  vi.mocked(inventoryApi.stockTakes.get).mockResolvedValue(data);
  renderPage(<StockTakeDetailPage />, { route: '/inventory/stock-takes/t1', path: '/inventory/stock-takes/:id' });
}

describe('StockTakeDetailPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('hides expected quantity on a blind take while counting', async () => {
    show('Manager', take({ blind: true, lines: [line({ expectedQty: null })] }));
    await screen.findByText('Flour');
    expect(screen.queryByText(/Expected/)).toBeNull();
  });

  it('shows expected on a non-blind count', async () => {
    show('Manager', take({}));
    expect(await screen.findByText(/Expected 10/)).toBeInTheDocument();
  });

  it('lists uncounted prominently and shows value impact for cost-visible roles', async () => {
    const lines = [
      line({ id: 'a', name: 'Sugar' }),
      line({ id: 'b', name: 'Flour', state: StockTakeLineState.Counted, countedQty: 8, varianceQty: -2, varianceValue: -4 }),
    ];
    show('Manager', take({ status: StockTakeStatus.Review, lines }));
    expect(await screen.findByText('1 not counted')).toBeInTheDocument();
    expect(screen.getByText('Value impact')).toBeInTheDocument();
  });

  it('omits value impact when costs are absent', async () => {
    const lines = [line({ state: StockTakeLineState.Counted, countedQty: 8, varianceQty: -2, varianceValue: null, unitCostSnapshot: null })];
    show('Manager', take({ status: StockTakeStatus.Review, lines }));
    await screen.findByText('Flour');
    expect(screen.queryByText('Value impact')).toBeNull();
  });

  it('apply opens the PIN dialog and a wrong PIN keeps it open with the error', async () => {
    show('Manager', take({ status: StockTakeStatus.Review }));
    vi.mocked(inventoryApi.stockTakes.apply).mockRejectedValue(new ApiError('validation', 'bad', { approverPin: ['That PIN was not accepted.'] }));
    fireEvent.click(await screen.findByRole('button', { name: 'Apply counts' }));
    const input = await screen.findByLabelText(/PIN/i);
    fireEvent.change(input, { target: { value: '1234' } });
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(await screen.findByText('That PIN was not accepted.')).toBeInTheDocument();
    expect(screen.getByLabelText(/PIN/i)).toBeInTheDocument();
  });

  it('Warehouse sees no Review or Apply buttons', async () => {
    show('Warehouse', take({ status: StockTakeStatus.Counting }));
    await screen.findByText('Flour');
    expect(screen.queryByRole('button', { name: 'Move to review' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Apply counts' })).toBeNull();
  });

  it('merges only the counted line from the response', async () => {
    const lines = [line({ id: 'a', name: 'Sugar', needsRecount: true, movementsSinceCount: 3 }), line({ id: 'b', name: 'Flour' })];
    show('Manager', take({ lines }));
    vi.mocked(inventoryApi.stockTakes.countLine).mockResolvedValue(
      take({ lines: [line({ id: 'a', name: 'Sugar' }), line({ id: 'b', name: 'Flour', state: StockTakeLineState.Counted, countedQty: 7 })] }),
    );
    fireEvent.change(await screen.findByLabelText('Counted quantity for Flour'), { target: { value: '7' } });
    fireEvent.click(screen.getAllByRole('button', { name: 'Save count' })[1]);
    await waitFor(() => expect(inventoryApi.stockTakes.countLine).toHaveBeenCalledWith('t1', 'b', { counted: 7, skip: false }));
    expect(await screen.findByText(/Stock moved by 3/)).toBeInTheDocument();
  });

  it('on 409 shows a reload message and refetches', async () => {
    show('Manager', take({}));
    vi.mocked(inventoryApi.stockTakes.countLine).mockRejectedValue(new ApiError('conflict', 'x'));
    fireEvent.change(await screen.findByLabelText('Counted quantity for Flour'), { target: { value: '7' } });
    const err = vi.spyOn(toast, 'error');
    fireEvent.click(screen.getByRole('button', { name: 'Save count' }));
    await waitFor(() => expect(err).toHaveBeenCalledWith(expect.stringMatching(/changed/i)));
    await waitFor(() => expect(inventoryApi.stockTakes.get).toHaveBeenCalledTimes(2));
  });
});
