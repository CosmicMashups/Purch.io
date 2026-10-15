import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { toast } from '../../components/feedback/toastStore';
import { ErrorState, describeQueryError } from '../../components/ErrorState';
import { EmptyState } from '../../components/EmptyState';
import { controlClass } from '../../components/forms/FormField';
import { PageHeader } from '../../components/PageHeader';
import { DataTable, type DataColumn } from '../../components/lists/DataTable';
import { SkeletonList } from '../../components/Skeleton';
import { StatusBadge } from '../../components/StatusBadge';
import { formatDateTime } from '../../lib/dates';
import { datedFilename, downloadTextFile } from '../../lib/download';
import { userMessage } from '../../lib/apiError';
import { useSession } from '../auth/useSession';
import { useBranches } from '../branches/queries';
import { useMembers } from '../business/memberQueries';
import { useDevices } from '../business/deviceQueries';
import { formatPeso } from '../dashboard/format';
import { usePosStore } from '../pos/posStore';
import { ordersApi, type OrderMethodName, type OrderRow, type OrderStatusName, type OrdersFilter } from './api';

const PAGE_SIZE = 25;

const STATUS_OPTIONS: { value: OrderStatusName; label: string }[] = [
  { value: 'Completed', label: 'Completed' },
  { value: 'Refunded', label: 'Refunded' },
  { value: 'Voided', label: 'Voided' },
  { value: 'Exchanged', label: 'Exchanged' },
];

const METHOD_OPTIONS: { value: OrderMethodName; label: string }[] = [
  { value: 'Cash', label: 'Cash' },
  { value: 'ManualGcashQr', label: 'GCash QR' },
  { value: 'QrPh', label: 'QR Ph' },
  { value: 'BankTransfer', label: 'Bank transfer' },
  { value: 'UtangCredit', label: 'Utang (credit)' },
  { value: 'BillPaymentELoad', label: 'Bill payment / e-load' },
  { value: 'Split', label: 'Split' },
];

const METHOD_LABEL = Object.fromEntries(METHOD_OPTIONS.map((o) => [o.value, o.label]));

/** `2026-10-08` for the viewer's local today. Dates in the filter are the viewer's own days, so "today" means their today. */
function localDay(date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** Start of the viewer's day as a UTC instant. `addDays` of 1 gives the exclusive end of that day. */
function dayStartIso(day: string, addDays = 0): string {
  const [y, m, d] = day.split('-').map(Number);
  return new Date(y, m - 1, d + addDays).toISOString();
}

function statusTone(row: OrderRow): 'success' | 'danger' | 'warning' | 'neutral' {
  if (row.status === 'Voided') return 'danger';
  if (row.status === 'Refunded') return 'warning';
  return 'success';
}

/**
 * Every finished sale of the business, newest first, for an Admin or Manager to look back over: filter by day, status,
 * branch, cashier or how it was paid, search by receipt number or customer, open the receipt, export what's shown.
 * The cashier's "Find a sale" stays the quick lookup at the till; this is the oversight view.
 */
export function OrdersPage() {
  const navigate = useNavigate();
  const showReceipt = usePosStore((s) => s.showReceipt);
  const isAdmin = useSession().role === 'Admin';
  const branches = useBranches();
  const members = useMembers();
  const devices = useDevices(isAdmin);

  const [fromDay, setFromDay] = useState(localDay());
  const [toDay, setToDay] = useState(localDay());
  const [status, setStatus] = useState<OrderStatusName | ''>('');
  const [branchId, setBranchId] = useState('');
  const [deviceId, setDeviceId] = useState('');
  const [staffUserId, setStaffUserId] = useState('');
  const [method, setMethod] = useState<OrderMethodName | ''>('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [opening, setOpening] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  // Searching for one receipt should not be held to today: a receipt number on its own looks across every day.
  const searching = search.trim() !== '';
  const filter = useMemo<OrdersFilter>(
    () => ({
      from: searching ? undefined : fromDay ? dayStartIso(fromDay) : undefined,
      to: searching ? undefined : toDay ? dayStartIso(toDay, 1) : undefined,
      status: status || undefined,
      branchId: branchId || undefined,
      deviceId: deviceId || undefined,
      staffUserId: staffUserId || undefined,
      method: method || undefined,
      search: search.trim() || undefined,
    }),
    [searching, fromDay, toDay, status, branchId, deviceId, staffUserId, method, search],
  );

  const orders = useQuery({
    queryKey: ['orders', filter, page],
    queryFn: () => ordersApi.list({ ...filter, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });

  const reset = <T,>(set: (value: T) => void) => (value: T) => {
    set(value);
    setPage(1);
  };

  async function openReceipt(row: OrderRow) {
    setOpening(row.id);
    try {
      const sale = await ordersApi.detail(row.id);
      showReceipt(sale);
      navigate('/sell/receipt', { state: { backTo: { to: '/business/orders', label: 'Orders' } } });
    } catch (error) {
      toast.error(userMessage(error));
    } finally {
      setOpening(null);
    }
  }

  async function exportCsv() {
    setExporting(true);
    try {
      downloadTextFile(datedFilename('orders', localDay()), await ordersApi.exportCsv(filter));
    } catch (error) {
      toast.error(userMessage(error));
    } finally {
      setExporting(false);
    }
  }

  const totalPages = orders.data ? Math.max(1, Math.ceil(orders.data.total / orders.data.pageSize)) : 1;
  const branchList = (branches.data ?? []).filter((b) => b.isActive !== false || b.id === branchId);

  const columns: DataColumn<OrderRow>[] = [
    {
      header: 'Receipt',
      sortValue: (r) => r.receiptNumber,
      cell: (r) => (
        <button
          type="button"
          onClick={() => void openReceipt(r)}
          disabled={opening === r.id}
          aria-label={`Open receipt ${r.receiptNumber ?? ''} ${r.deviceName}`.trim()}
          className="font-bold tabular-nums text-brand-strong underline disabled:opacity-60"
        >
          {r.receiptNumber ? `#${r.receiptNumber}` : 'No receipt'}
        </button>
      ),
    },
    { header: 'Date', className: 'whitespace-nowrap text-gray-600', sortValue: (r) => new Date(r.at), cell: (r) => formatDateTime(r.at) },
    {
      header: 'Status',
      sortValue: (r) => r.status,
      cell: (r) => <StatusBadge label={r.hasExchange ? `${r.status}, exchanged` : r.status} tone={statusTone(r)} />,
    },
    { header: 'Branch', className: 'text-gray-600', sortValue: (r) => r.branchName, cell: (r) => r.branchName },
    { header: 'Device', className: 'text-gray-600', sortValue: (r) => r.deviceName, cell: (r) => r.deviceName },
    { header: 'Cashier', className: 'text-gray-600', sortValue: (r) => r.staffName, cell: (r) => r.staffName },
    { header: 'Customer', className: 'text-gray-600', sortValue: (r) => r.customerName, cell: (r) => r.customerName },
    {
      header: 'Paid with',
      className: 'text-gray-600',
      sortValue: (r) => r.paymentMethods.map((m) => METHOD_LABEL[m] ?? m).join(' + '),
      cell: (r) => r.paymentMethods.map((m) => METHOD_LABEL[m] ?? m).join(' + '),
    },
    { header: 'Total', className: 'text-right font-semibold tabular-nums', sortValue: (r) => r.totalAmount, cell: (r) => formatPeso(r.totalAmount) },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Orders"
        subtitle="Every sale and its receipt."
        backTo={{ to: '/business', label: 'Business' }}
        action={
          <button
            type="button"
            onClick={() => void exportCsv()}
            disabled={exporting || !orders.data || orders.data.total === 0}
            className="h-12 rounded-control bg-brand text-on-brand hover:bg-brand-strong px-6 text-base font-semibold disabled:opacity-50"
          >
            {exporting ? 'Exporting…' : 'Export CSV'}
          </button>
        }
      />

      <form onSubmit={(e) => e.preventDefault()} aria-label="Order filters" className="grid gap-3 rounded-panel border border-line bg-surface p-4 sm:grid-cols-2 lg:grid-cols-4">
        <label className="flex flex-col gap-1 text-sm font-semibold sm:col-span-2 lg:col-span-4">
          Find an order
          <input
            type="search"
            value={search}
            onChange={(e) => reset(setSearch)(e.target.value)}
            placeholder="Receipt number or customer name"
            className={controlClass}
          />
          {searching && <span className="text-sm font-normal text-ink-soft">Searching every day. Clear this to go back to the dates below.</span>}
        </label>
        <label className="flex flex-col gap-1 text-sm font-semibold">
          From
          <input type="date" value={fromDay} max={toDay || undefined} disabled={searching} onChange={(e) => reset(setFromDay)(e.target.value)} className={controlClass} />
        </label>
        <label className="flex flex-col gap-1 text-sm font-semibold">
          To
          <input type="date" value={toDay} min={fromDay || undefined} disabled={searching} onChange={(e) => reset(setToDay)(e.target.value)} className={controlClass} />
        </label>
        <label className="flex flex-col gap-1 text-sm font-semibold">
          Status
          <select value={status} onChange={(e) => reset(setStatus)(e.target.value as OrderStatusName | '')} className={controlClass}>
            <option value="">All</option>
            {STATUS_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm font-semibold">
          Paid with
          <select value={method} onChange={(e) => reset(setMethod)(e.target.value as OrderMethodName | '')} className={controlClass}>
            <option value="">Any</option>
            {METHOD_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </label>
        {branchList.length > 1 && (
          <label className="flex flex-col gap-1 text-sm font-semibold">
            Branch
            <select value={branchId} onChange={(e) => reset(setBranchId)(e.target.value)} className={controlClass}>
              <option value="">All branches</option>
              {branchList.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {(members.data?.length ?? 0) > 0 && (
          <label className="flex flex-col gap-1 text-sm font-semibold">
            Cashier
            <select value={staffUserId} onChange={(e) => reset(setStaffUserId)(e.target.value)} className={controlClass}>
              <option value="">Anyone</option>
              {members.data?.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {isAdmin && (devices.data?.length ?? 0) > 0 && (
          <label className="flex flex-col gap-1 text-sm font-semibold">
            Device
            <select value={deviceId} onChange={(e) => reset(setDeviceId)(e.target.value)} className={controlClass}>
              <option value="">Any device</option>
              {devices.data?.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name ?? d.deviceIdentifier ?? 'Device'}
                </option>
              ))}
            </select>
          </label>
        )}
      </form>

      {orders.isPending && <SkeletonList />}
      {orders.isError && <ErrorState title="Orders could not be loaded" message={describeQueryError(orders.error)} onRetry={() => void orders.refetch()} />}
      {orders.data && orders.data.items.length === 0 && (
        <EmptyState title="No orders match" description={searching ? 'Check the receipt number or name.' : 'Try other dates or clear a filter.'} />
      )}
      {orders.data && orders.data.items.length > 0 && (
        <>
          <p className="text-sm text-ink-soft" aria-live="polite">
            {orders.data.total} {orders.data.total === 1 ? 'order' : 'orders'}
          </p>
          <DataTable label="Orders" rows={orders.data.items} columns={columns} getId={(r) => r.id} />
          {totalPages > 1 && (
            <nav aria-label="Pages" className="flex items-center justify-between gap-3">
              <button type="button" disabled={page <= 1} onClick={() => setPage(page - 1)} className="h-12 rounded-control border border-line px-5 text-base font-semibold hover:border-brand disabled:opacity-40">
                Previous
              </button>
              <span className="text-sm text-ink-soft">
                Page {page} of {totalPages}
              </span>
              <button type="button" disabled={page >= totalPages} onClick={() => setPage(page + 1)} className="h-12 rounded-control border border-line px-5 text-base font-semibold hover:border-brand disabled:opacity-40">
                Next
              </button>
            </nav>
          )}
        </>
      )}
    </div>
  );
}
