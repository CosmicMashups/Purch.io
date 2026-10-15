import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { toast } from '../../../components/feedback/toastStore';
import { BarList } from '../../../components/charts/RankedCharts';
import { csvRowCount, datedFilename, downloadTextFile } from '../../../lib/download';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { formatPeso } from '../../dashboard/format';
import { reportsApi } from '../api';
import { toCsv } from '../csv';
import { useDiscountReport, useManualDiscounts, useRefundReport, useVoidReport } from '../queries';
import { businessToday } from '../range';
import type { ExchangeRow, ManualDiscountRow, RefundRow, VoidRow } from '../revenueTypes';
import type { RangeParams } from '../types';
import { PrintHeader, ReportActions, ReportTable, Stat } from './ReportParts';

const PAGE = 50;
const when = (iso: string) => new Date(iso).toLocaleString('en-PH', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Manila' });
const dash = (v: string | null | undefined) => v ?? '–';

/** Server-built listings (names every staff member and approver) are Admin only; the aggregate figures are not. */
function useListingCsv(fetcher: (p: RangeParams) => Promise<string>, prefix: string, params: RangeParams) {
  const mutation = useMutation({ mutationFn: () => fetcher(params) });
  return {
    busy: mutation.isPending,
    run: () =>
      mutation.mutate(undefined, {
        onSuccess: (csv) => {
          const rows = csvRowCount(csv);
          if (rows === 0) return toast.info('There is nothing in this period to export.');
          downloadTextFile(datedFilename(prefix, businessToday()), csv);
          toast.success(`Exported ${rows} row${rows === 1 ? '' : 's'}`);
        },
      }),
  };
}

function ShowMore({ shown, total, onMore }: { shown: number; total: number; onMore: () => void }) {
  if (shown >= total) return null;
  return (
    <button type="button" onClick={onMore} className="mt-4 inline-flex h-12 items-center rounded-control border border-line bg-surface px-5 text-base font-semibold hover:border-brand print:hidden">
      Show more ({shown} of {total})
    </button>
  );
}

export function DiscountsPanel({ params, isAdmin }: { params: RangeParams; isAdmin: boolean }) {
  const [limit, setLimit] = useState(PAGE);
  const summary = useDiscountReport(params);
  const manual = useManualDiscounts(params, limit);
  const listing = useListingCsv(reportsApi.manualDiscountsCsv, 'manual-discounts', params);
  const d = summary.data;

  const csv = () =>
    d
      ? toCsv(
          ['Section', 'Name', 'Sales', 'Discounts', 'Rate %'],
          [
            ['Type', 'Promo code', '', d.promoCodeDiscount, ''],
            ['Type', 'Item promo', '', d.itemPromoDiscount, ''],
            ['Type', 'Manual', '', d.manualDiscount, ''],
            ['Type', 'Senior/PWD', '', d.seniorPwdDiscount, ''],
            ...d.byCashier.map((c) => ['Cashier', c.name, c.sales, c.discounts, c.ratePercent]),
          ],
        )
      : null;

  return (
    <div className="flex flex-col gap-6">
      <PrintHeader title="Discount report" params={params} />
      <div className="flex justify-end">
        <ReportActions filename="discounts" buildCsv={csv} disabled={!d} listing={isAdmin ? { label: 'Download manual discounts CSV', busy: listing.busy, onClick: listing.run } : undefined} />
      </div>
      {d && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
          <Stat label="Total discounts" value={formatPeso(d.totalDiscounts)} note={`${d.discountRatePercent.toFixed(1)}% of sales before discounts`} />
          <Stat label="Promo code" value={formatPeso(d.promoCodeDiscount)} />
          <Stat label="Item promos" value={formatPeso(d.itemPromoDiscount)} />
          <Stat label="Manual" value={formatPeso(d.manualDiscount)} />
          <Stat label="Senior/PWD" value={formatPeso(d.seniorPwdDiscount)} />
        </div>
      )}
      <div className="grid gap-6 lg:grid-cols-2">
        <AsyncPanel title="Discounts by cashier" subtitle="Total discounts given on each cashier's sales" query={summary} isEmpty={(r) => r.byCashier.length === 0} emptyMessage="No sales in this period.">
          {(r) => (
            <BarList
              formatValue={formatPeso}
              rows={r.byCashier.map((c) => ({ key: c.staffUserId ?? c.name, label: c.name, value: c.discounts, detail: `${c.ratePercent.toFixed(1)}% of ${c.saleCount} sale${c.saleCount === 1 ? '' : 's'}` }))}
            />
          )}
        </AsyncPanel>
        <AsyncPanel title="Approvers" subtitle="Who signed off manual discounts and price overrides" query={summary} isEmpty={(r) => r.approvers.length === 0} emptyMessage="No manual discounts or price overrides were approved.">
          {(r) => (
            <ReportTable
              caption="Approvers"
              rowKey={(a) => a.userId}
              rows={r.approvers}
              columns={[
                { header: 'Approver', cell: (a) => a.name },
                { header: 'Discounts', numeric: true, cell: (a) => a.discountApprovals },
                { header: 'Price overrides', numeric: true, cell: (a) => a.priceOverrideApprovals },
              ]}
            />
          )}
        </AsyncPanel>
      </div>
      <AsyncPanel title="Manual discounts and price overrides" subtitle="Sales that carried one, newest first" query={manual} isEmpty={(m) => m.rows.length === 0} emptyMessage="No manual discounts or price overrides in this period.">
        {(m) => (
          <>
            <ReportTable<ManualDiscountRow>
              caption="Manual discounts"
              rowKey={(r) => r.transactionId}
              rows={m.rows}
              columns={[
                { header: 'Sold', cell: (r) => when(r.completedAt) },
                { header: 'Receipt', cell: (r) => (r.receiptNumber === null ? '–' : r.receiptNumber) },
                { header: 'Cashier', cell: (r) => r.staffName },
                { header: 'Discount', numeric: true, cell: (r) => formatPeso(r.cartDiscount + r.lineDiscounts) },
                { header: 'Overrides', numeric: true, cell: (r) => r.priceOverrideCount },
                { header: 'Reason', cell: (r) => dash(r.reason) },
                { header: 'Approved by', cell: (r) => dash(r.approverName) },
              ]}
            />
            <ShowMore shown={m.rows.length} total={m.total} onMore={() => setLimit((n) => n + PAGE)} />
          </>
        )}
      </AsyncPanel>
    </div>
  );
}

export function VoidsRefundsPanel({ params, isAdmin }: { params: RangeParams; isAdmin: boolean }) {
  const [voidLimit, setVoidLimit] = useState(PAGE);
  const [refundLimit, setRefundLimit] = useState(PAGE);
  const voids = useVoidReport(params, voidLimit);
  const refunds = useRefundReport(params, refundLimit);
  const voidCsv = useListingCsv(reportsApi.voidsCsv, 'voids', params);
  const refundCsv = useListingCsv(reportsApi.refundsCsv, 'refunds', params);
  const v = voids.data;
  const r = refunds.data;

  const csv = () =>
    v && r
      ? toCsv(
          ['Type', 'When', 'Branch', 'Staff', 'Total', 'Reason', 'Approver'],
          [
            ...v.voids.rows.map((x) => ['Void', x.voidedAt, x.branchName, x.staffName, x.total, x.reason, x.approverName]),
            ...r.refunds.rows.map((x) => ['Refund', x.refundedAt, x.branchName, x.staffName, x.total, x.reason, x.approverName]),
          ],
        )
      : null;

  return (
    <div className="flex flex-col gap-6">
      <PrintHeader title="Voids and refunds" params={params} />
      <div className="flex flex-wrap justify-end gap-3">
        <ReportActions filename="voids-and-refunds" buildCsv={csv} disabled={!v || !r} />
        {isAdmin && (
          <div className="flex flex-wrap gap-3 print:hidden">
            <button type="button" disabled={voidCsv.busy} onClick={voidCsv.run} className="inline-flex h-12 items-center rounded-control border border-line bg-surface px-5 text-base font-semibold hover:border-brand disabled:opacity-50">
              {voidCsv.busy ? 'Preparing...' : 'Download all voids CSV'}
            </button>
            <button type="button" disabled={refundCsv.busy} onClick={refundCsv.run} className="inline-flex h-12 items-center rounded-control border border-line bg-surface px-5 text-base font-semibold hover:border-brand disabled:opacity-50">
              {refundCsv.busy ? 'Preparing...' : 'Download all refunds CSV'}
            </button>
          </div>
        )}
      </div>

      {v && r && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label="Voided" value={formatPeso(v.amount)} note={`${v.count} cart${v.count === 1 ? '' : 's'} with items`} />
          <Stat label="Refunded" value={formatPeso(r.refundAmount)} note={`${r.refundCount} sale${r.refundCount === 1 ? '' : 's'}`} />
          <Stat label="Exchange differences" value={formatPeso(r.exchangePriceDifference)} />
          <Stat label="Not counted as voids" value={String(v.emptyDiscardedCount + v.expiredParkedCount)} note={`${v.emptyDiscardedCount} empty, ${v.expiredParkedCount} expired parked`} />
        </div>
      )}

      <AsyncPanel title="Voids" subtitle="Carts with items that were cancelled before payment. Empty and expired parked carts are left out." query={voids} isEmpty={(d) => d.voids.rows.length === 0} emptyMessage="No voids in this period.">
        {(d) => (
          <>
            <ReportTable<VoidRow>
              caption="Voids"
              rowKey={(x) => x.transactionId}
              rows={d.voids.rows}
              columns={[
                { header: 'When', cell: (x) => when(x.voidedAt) },
                { header: 'Cashier', cell: (x) => x.staffName },
                { header: 'Items', numeric: true, cell: (x) => x.lineCount },
                { header: 'Total', numeric: true, cell: (x) => formatPeso(x.total) },
                { header: 'Reason', cell: (x) => dash(x.reason) },
                { header: 'Approved by', cell: (x) => dash(x.approverName) },
              ]}
            />
            <ShowMore shown={d.voids.rows.length} total={d.voids.total} onMore={() => setVoidLimit((n) => n + PAGE)} />
          </>
        )}
      </AsyncPanel>

      <AsyncPanel title="Refunds" subtitle="By the day the refund was made" query={refunds} isEmpty={(d) => d.refunds.rows.length === 0} emptyMessage="No refunds in this period.">
        {(d) => (
          <>
            <ReportTable<RefundRow>
              caption="Refunds"
              rowKey={(x) => x.transactionId}
              rows={d.refunds.rows}
              columns={[
                { header: 'Refunded', cell: (x) => when(x.refundedAt) },
                { header: 'Receipt', cell: (x) => (x.receiptNumber === null ? '–' : x.receiptNumber) },
                { header: 'Cashier', cell: (x) => x.staffName },
                { header: 'Total', numeric: true, cell: (x) => formatPeso(x.total) },
                { header: 'Reason', cell: (x) => dash(x.reason) },
                { header: 'Approved by', cell: (x) => dash(x.approverName) },
              ]}
            />
            <ShowMore shown={d.refunds.rows.length} total={d.refunds.total} onMore={() => setRefundLimit((n) => n + PAGE)} />
          </>
        )}
      </AsyncPanel>

      <AsyncPanel title="Exchanges" subtitle="Items returned and replaced; the price difference was settled at the till" query={refunds} isEmpty={(d) => d.exchanges.rows.length === 0} emptyMessage="No exchanges in this period.">
        {(d) => (
          <ReportTable<ExchangeRow>
            caption="Exchanges"
            rowKey={(x) => x.adjustmentId}
            rows={d.exchanges.rows}
            columns={[
              { header: 'When', cell: (x) => when(x.createdAt) },
              { header: 'Receipt', cell: (x) => (x.originalReceiptNumber === null ? '–' : x.originalReceiptNumber) },
              { header: 'Returned', numeric: true, cell: (x) => formatPeso(x.returnedTotal) },
              { header: 'Replacement', numeric: true, cell: (x) => formatPeso(x.replacementTotal) },
              { header: 'Difference', numeric: true, cell: (x) => formatPeso(x.priceDifference) },
              { header: 'Reason', cell: (x) => x.reason },
              { header: 'Approved by', cell: (x) => x.approverName },
            ]}
          />
        )}
      </AsyncPanel>
    </div>
  );
}
