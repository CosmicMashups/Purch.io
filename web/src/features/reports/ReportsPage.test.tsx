import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { ApiError } from '../../lib/apiError';
import * as download from '../../lib/download';
import { renderPage, signInAs } from '../../test/render';
import { branchesApi } from '../branches/api';
import { reportsApi } from './api';
import { ReportsPage } from './ReportsPage';
import { BirReadingType, type BirReading } from './types';

vi.mock('./api', () => ({
  reportsApi: {
    staffPerformance: vi.fn(),
    departmentSales: vi.fn(),
    movementSummary: vi.fn(),
    xReading: vi.fn(),
    zReading: vi.fn(),
    lowStockCsv: vi.fn(),
    transactionsCsv: vi.fn(),
  },
}));
vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../../lib/download', async (importActual) => ({ ...(await importActual<typeof import('../../lib/download')>()), downloadTextFile: vi.fn() }));

const reading = (over: Partial<BirReading> = {}): BirReading => ({
  type: BirReadingType.X,
  deviceId: 'dev1',
  machineIdentificationNumber: 'MIN-0001',
  generatedAt: '2026-09-24T10:00:00Z',
  beginningReceiptNumber: 100,
  endingReceiptNumber: 141,
  transactionCount: 42,
  grossSales: 12345.5,
  vatableSales: 11000,
  vatAmount: 1320,
  seniorPwdDiscountTotal: 200,
  promoDiscountTotal: 150,
  totalDiscounts: 350,
  netSales: 11995.5,
  voidedCount: 1,
  voidedAmount: 90,
  oldGrandAccumulatedSales: 500000,
  newGrandAccumulatedSales: 511995.5,
  resetCounter: 7,
  lateReceiptNumbers: [98],
  missingReceiptNumbers: [120, 121],
  ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  signInAs('Manager', { device_id: 'dev1', branch_id: 'kat', scope_type: 'Tenant' });
  vi.mocked(branchesApi.list).mockResolvedValue([
    { id: 'kat', name: 'Katipunan', address: null },
    { id: 'kam', name: 'Kamuning', address: null },
  ]);
  vi.mocked(reportsApi.staffPerformance).mockResolvedValue({
    sales: [
      { staffUserId: 'a', staffName: 'Ana Reyes', transactionCount: 30, totalSales: 9000 },
      { staffUserId: 'b', staffName: 'Ben Cruz', transactionCount: 1, totalSales: 4500 },
    ],
    shiftAttendance: [{ staffUserId: 'a', staffName: 'Ana Reyes', shiftsOpened: 5, shiftsWithVariance: 2 }],
  });
  vi.mocked(reportsApi.departmentSales).mockResolvedValue([
    { departmentId: null, departmentName: 'General', revenue: 8000 },
    { departmentId: 'd1', departmentName: 'Bakery', revenue: 16000 },
  ]);
  vi.mocked(reportsApi.movementSummary).mockResolvedValue({ from: '', to: '', byType: [{ type: 3, totalQuantity: 12, movementCount: 1 }] });
});

describe('range reports', () => {
  it('shows staff sales ranked, with the server figures and pluralised sale counts', async () => {
    renderPage(<ReportsPage />);
    expect(await screen.findByText('₱9,000.00')).toBeInTheDocument();
    expect(screen.getByText('30 sales')).toBeInTheDocument();
    expect(screen.getByText('₱4,500.00')).toBeInTheDocument();
    expect(screen.getByText('1 sale')).toBeInTheDocument();
    const names = screen.getAllByRole('listitem').map((li) => li.textContent ?? '');
    expect(names.findIndex((t) => t.includes('Ana Reyes'))).toBeLessThan(names.findIndex((t) => t.includes('Ben Cruz')));
    expect(await screen.findByText('2 shifts closed with a cash difference')).toBeInTheDocument();
    expect(screen.getByRole('meter', { name: 'Ana Reyes' })).toHaveAttribute('aria-valuenow', '2');
    expect(screen.getByRole('meter', { name: 'Ana Reyes' })).toHaveAttribute('aria-valuemax', '5');
  });

  it('asks the API for the last 30 days in Manila time by default', async () => {
    renderPage(<ReportsPage />);
    await screen.findByText('30 sales');
    const params = vi.mocked(reportsApi.staffPerformance).mock.calls[0][0];
    expect(params).not.toHaveProperty('branchId');
    expect(new Date(params.from).toISOString().endsWith('T16:00:00.000Z')).toBe(true);
    expect((new Date(params.to).getTime() - new Date(params.from).getTime()) / 86_400_000).toBe(30);
  });

  it('re-queries when a preset or a branch is chosen', async () => {
    renderPage(<ReportsPage />);
    await screen.findByText('30 sales');
    fireEvent.click(screen.getByRole('button', { name: 'Today' }));
    await waitFor(() => {
      const last = vi.mocked(reportsApi.staffPerformance).mock.calls.at(-1)![0];
      expect((new Date(last.to).getTime() - new Date(last.from).getTime()) / 86_400_000).toBe(1);
    });
    fireEvent.change(screen.getByLabelText('Branch'), { target: { value: 'kam' } });
    await waitFor(() => expect(vi.mocked(reportsApi.staffPerformance).mock.calls.at(-1)![0]).toMatchObject({ branchId: 'kam' }));
  });

  it('does not fetch while a custom range is reversed, and says why', async () => {
    renderPage(<ReportsPage />);
    await screen.findByText('30 sales');
    fireEvent.click(screen.getByRole('button', { name: 'Custom' }));
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-09-10' } });
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-09-20' } });
    await waitFor(() => expect(vi.mocked(reportsApi.staffPerformance).mock.calls.at(-1)![0].from).toBe('2026-09-09T16:00:00.000Z'));
    // The range is valid up to here and fetches; from now on it is reversed and must not.
    vi.mocked(reportsApi.staffPerformance).mockClear();
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-09-01' } });
    expect(await screen.findByText('The end date is before the start date')).toBeInTheDocument();
    expect(screen.getByText('Choose a valid date range to see this report.')).toBeInTheDocument();
    expect(reportsApi.staffPerformance).not.toHaveBeenCalled();
  });

  it('shows departments with General for items without one', async () => {
    renderPage(<ReportsPage />, { route: '/?tab=departments' });
    expect(await screen.findByText('General')).toBeInTheDocument();
    expect(screen.getByText('₱16,000.00')).toBeInTheDocument();
  });

  it('labels stock movements by reason', async () => {
    renderPage(<ReportsPage />, { route: '/?tab=stock' });
    expect(await screen.findByText('Spoiled')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('1 record')).toBeInTheDocument();
  });

  it('shows a retryable error rather than an empty report when loading fails', async () => {
    vi.mocked(reportsApi.departmentSales).mockRejectedValue(new Error('boom'));
    renderPage(<ReportsPage />, { route: '/?tab=departments' });
    expect(await screen.findByText('Sales by department is unavailable')).toBeInTheDocument();
    expect(screen.queryByText('No sales in this period.')).not.toBeInTheDocument();
  });
});

describe('X and Z readings', () => {
  it('needs a paired device', async () => {
    signInAs('Admin');
    renderPage(<ReportsPage />, { route: '/?tab=bir' });
    expect(await screen.findByText('Sign in with a device to sell')).toBeInTheDocument();
    expect(reportsApi.xReading).not.toHaveBeenCalled();
  });

  it('takes an X-reading and shows every server figure', async () => {
    vi.mocked(reportsApi.xReading).mockResolvedValue(reading());
    renderPage(<ReportsPage />, { route: '/?tab=bir' });
    fireEvent.click(await screen.findByRole('button', { name: 'Take X-reading' }));

    const card = await screen.findByRole('article', { name: 'X-reading' });
    expect(within(card).getByText('100 to 141')).toBeInTheDocument();
    expect(within(card).getByText('₱12,345.50')).toBeInTheDocument();
    expect(within(card).getByText('₱11,995.50')).toBeInTheDocument();
    expect(within(card).getByText('98')).toBeInTheDocument();
    expect(within(card).getByText('120, 121')).toBeInTheDocument();
    expect(screen.getByText(/not yet checked against BIR accreditation/)).toBeInTheDocument();
    expect(reportsApi.zReading).not.toHaveBeenCalled();
  });

  it('will not take a Z-reading until it is confirmed, and backing out changes nothing', async () => {
    renderPage(<ReportsPage />, { route: '/?tab=bir' });
    fireEvent.click(await screen.findByRole('button', { name: 'Take Z-reading' }));
    const dialog = screen.getByRole('dialog', { name: 'Take the Z-reading?' });
    expect(reportsApi.zReading).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(reportsApi.zReading).not.toHaveBeenCalled();
  });

  it('takes the Z-reading once confirmed and labels it as end of day', async () => {
    vi.mocked(reportsApi.zReading).mockResolvedValue(reading({ type: BirReadingType.Z }));
    renderPage(<ReportsPage />, { route: '/?tab=bir' });
    fireEvent.click(await screen.findByRole('button', { name: 'Take Z-reading' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Take Z-reading' }));
    expect(await screen.findByRole('article', { name: 'Z-reading' })).toBeInTheDocument();
    expect(reportsApi.zReading).toHaveBeenCalledTimes(1);
  });

  it('shows the server refusal and no reading when it fails', async () => {
    vi.mocked(reportsApi.xReading).mockRejectedValue(new ApiError('forbidden', 'nope'));
    renderPage(<ReportsPage />, { route: '/?tab=bir' });
    fireEvent.click(await screen.findByRole('button', { name: 'Take X-reading' }));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toBe('You do not have permission to do that.'));
    expect(screen.queryByRole('article')).not.toBeInTheDocument();
  });
});

describe('exports', () => {
  it('downloads the low-stock list with a dated name and reports how many items', async () => {
    vi.mocked(reportsApi.lowStockCsv).mockResolvedValue('Item,Stock On Hand\r\nMilk,2\r\nBeans,0\r\n');
    renderPage(<ReportsPage />, { route: '/?tab=exports' });
    fireEvent.click((await screen.findAllByRole('button', { name: 'Download CSV' }))[0]);
    await waitFor(() => expect(download.downloadTextFile).toHaveBeenCalledTimes(1));
    expect(vi.mocked(download.downloadTextFile).mock.calls[0][0]).toMatch(/^low-stock-reorder-\d{4}-\d{2}-\d{2}\.csv$/);
    expect(useToastStore.getState().toasts[0].message).toBe('Exported 2 items to reorder');
  });

  it('downloads nothing when no item needs reordering', async () => {
    vi.mocked(reportsApi.lowStockCsv).mockResolvedValue('Item,Stock On Hand\r\n');
    renderPage(<ReportsPage />, { route: '/?tab=exports' });
    fireEvent.click((await screen.findAllByRole('button', { name: 'Download CSV' }))[0]);
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toMatch(/Nothing is below its reorder level/));
    expect(download.downloadTextFile).not.toHaveBeenCalled();
  });

  it('keeps the raw sales export for admins only', async () => {
    renderPage(<ReportsPage />, { route: '/?tab=exports' });
    expect(await screen.findByText('Only an admin can export raw sales.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Download CSV' })).toHaveLength(1);
  });

  it('lets an admin export sales for the chosen period and branch', async () => {
    signInAs('Admin', { device_id: 'dev1', branch_id: 'kat', scope_type: 'Tenant' });
    vi.mocked(reportsApi.transactionsCsv).mockResolvedValue('Receipt,Total\r\n41,137.5\r\n');
    renderPage(<ReportsPage />, { route: '/?tab=exports' });
    fireEvent.change(await screen.findByLabelText('Branch'), { target: { value: 'kam' } });
    fireEvent.click(screen.getAllByRole('button', { name: 'Download CSV' })[1]);
    await waitFor(() => expect(reportsApi.transactionsCsv).toHaveBeenCalledTimes(1));
    expect(vi.mocked(reportsApi.transactionsCsv).mock.calls[0][0]).toMatchObject({ branchId: 'kam' });
    expect(download.downloadTextFile).toHaveBeenCalledTimes(1);
  });
});
