import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { branchesApi } from '../branches/api';
import { deviceApi } from '../business/deviceApi';
import { memberApi } from '../business/memberApi';
import { usePosStore } from '../pos/posStore';
import type { Transaction } from '../pos/types';
import { ordersApi, type OrderRow } from './api';
import { OrdersPage } from './OrdersPage';

vi.mock('./api', () => ({ ordersApi: { list: vi.fn(), detail: vi.fn(), exportCsv: vi.fn() } }));
vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../business/deviceApi', () => ({ deviceApi: { list: vi.fn() } }));
vi.mock('../business/memberApi', () => ({ memberApi: { list: vi.fn() } }));
vi.mock('../../lib/download', () => ({ downloadTextFile: vi.fn(), datedFilename: (p: string) => `${p}.csv` }));

const row = (over: Partial<OrderRow> = {}): OrderRow => ({
  id: 's1',
  receiptNumber: 1047,
  at: '2026-10-08T03:15:00Z',
  status: 'Completed',
  hasExchange: false,
  branchId: 'kat',
  branchName: 'Katipunan',
  deviceId: 'd1',
  deviceName: 'Front counter',
  staffName: 'Ben Santos',
  customerName: null,
  paymentMethods: ['Cash'],
  totalAmount: 250,
  ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  usePosStore.setState({ receipt: null });
  signInAs('Manager');
  vi.mocked(branchesApi.list).mockResolvedValue([
    { id: 'kat', name: 'Katipunan', address: null },
    { id: 'qc', name: 'Cubao', address: null },
  ]);
  vi.mocked(memberApi.list).mockResolvedValue([]);
  vi.mocked(deviceApi.list).mockResolvedValue([]);
  vi.mocked(ordersApi.list).mockResolvedValue({ items: [row(), row({ id: 's2', receiptNumber: 1047, deviceId: 'd2', deviceName: 'Back counter', status: 'Refunded', totalAmount: 90 })], total: 2, page: 1, pageSize: 25 });
});

describe('OrdersPage', () => {
  it('lists sales with their device, since receipt numbers repeat across devices, and starts on today', async () => {
    renderPage(<OrdersPage />);
    expect(await screen.findAllByText('#1047')).toHaveLength(2);
    expect(screen.getByText(/Front counter/)).toBeInTheDocument();
    expect(screen.getByText(/Back counter/)).toBeInTheDocument();
    expect(within(screen.getByRole('list', { name: 'Orders' })).getByText('Refunded')).toBeInTheDocument();
    const first = vi.mocked(ordersApi.list).mock.calls[0][0];
    expect(first.from).toBeTruthy();
    expect(first.to).toBeTruthy();
    expect(first.page).toBe(1);
  });

  it('narrows by status and payment method', async () => {
    renderPage(<OrdersPage />);
    await screen.findAllByText('#1047');
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Refunded' } });
    await waitFor(() => expect(vi.mocked(ordersApi.list).mock.calls.at(-1)![0].status).toBe('Refunded'));
    fireEvent.change(screen.getByLabelText('Paid with'), { target: { value: 'UtangCredit' } });
    await waitFor(() => expect(vi.mocked(ordersApi.list).mock.calls.at(-1)![0].method).toBe('UtangCredit'));
  });

  it('searching by receipt number looks across every day instead of only the chosen dates', async () => {
    renderPage(<OrdersPage />);
    await screen.findAllByText('#1047');
    fireEvent.change(screen.getByLabelText(/Find an order/), { target: { value: '1047' } });
    await waitFor(() => {
      const last = vi.mocked(ordersApi.list).mock.calls.at(-1)![0];
      expect(last.search).toBe('1047');
      expect(last.from).toBeUndefined();
      expect(last.to).toBeUndefined();
    });
    expect(screen.getByLabelText('From')).toBeDisabled();
  });

  it('opens the receipt of the sale chosen and comes back to Orders from it', async () => {
    const sale = { id: 's1', receiptNumber: 1047, status: 2, lines: [], payments: [] } as unknown as Transaction;
    vi.mocked(ordersApi.detail).mockResolvedValue(sale);
    renderPage(<OrdersPage />, { otherRoutes: [{ path: '/sell/receipt', element: <p>Receipt screen</p> }] });
    fireEvent.click((await screen.findAllByRole('button', { name: /Open receipt 1047 Front counter/ }))[0]);
    expect(await screen.findByText('Receipt screen')).toBeInTheDocument();
    expect(ordersApi.detail).toHaveBeenCalledWith('s1');
    expect(usePosStore.getState().receipt?.id).toBe('s1');
  });

  it('says so when nothing matches, rather than showing an empty page', async () => {
    vi.mocked(ordersApi.list).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });
    renderPage(<OrdersPage />);
    expect(await screen.findByText('No orders match')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Export CSV' })).toBeDisabled();
  });

  it('pages through a long list', async () => {
    vi.mocked(ordersApi.list).mockResolvedValue({ items: [row()], total: 60, page: 1, pageSize: 25 });
    renderPage(<OrdersPage />);
    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => expect(vi.mocked(ordersApi.list).mock.calls.at(-1)![0].page).toBe(2));
  });

  it('exports what is on screen as a CSV', async () => {
    vi.mocked(ordersApi.exportCsv).mockResolvedValue('Receipt,Date\n');
    const { downloadTextFile } = await import('../../lib/download');
    renderPage(<OrdersPage />);
    await screen.findAllByText('#1047');
    fireEvent.click(screen.getByRole('button', { name: 'Export CSV' }));
    await waitFor(() => expect(downloadTextFile).toHaveBeenCalledWith('orders.csv', 'Receipt,Date\n'));
  });

  it('shows a retryable error', async () => {
    vi.mocked(ordersApi.list).mockRejectedValue(new Error('boom'));
    renderPage(<OrdersPage />);
    expect(await screen.findByText('Orders could not be loaded')).toBeInTheDocument();
  });
});
