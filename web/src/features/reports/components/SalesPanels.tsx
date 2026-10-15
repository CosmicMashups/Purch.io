import { useState } from 'react';
import { AreaLineChart } from '../../../components/charts/AreaLineChart';
import { paretoRows } from '../../../components/charts/chartData';
import { assignColors } from '../../../components/charts/colors';
import { ComboChart } from '../../../components/charts/ComboChart';
import { Donut, type DonutSlice } from '../../../components/charts/Donut';
import { ParetoChart } from '../../../components/charts/ParetoChart';
import { BarList } from '../../../components/charts/RankedCharts';
import { ScatterChart } from '../../../components/charts/ScatterChart';
import { StackedColumnChart } from '../../../components/charts/StackedColumnChart';
import { WaterfallChart } from '../../../components/charts/WaterfallChart';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { formatDay, formatPeso, formatPesoCompact } from '../../dashboard/format';
import { toCsv } from '../csv';
import { useProfitReport, useSalesMix, useTaxReport } from '../queries';
import { PAYMENT_METHOD_LABELS, type ProfitGroup, type TaxDay } from '../revenueTypes';
import type { RangeParams } from '../types';
import { PrintHeader, ReportActions, ReportTable, Stat, SubViewTabs } from './ReportParts';

const pct = (v: number | null) => (v === null ? 'n/a' : `${v.toFixed(1)}%`);
const hourLabel = (h: number) => `${h % 12 === 0 ? 12 : h % 12} ${h < 12 ? 'AM' : 'PM'}`;
const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

type MixView = 'payment' | 'category' | 'hour';

function paymentSlices(payments: { method: number; amount: number }[]): DonutSlice[] {
  const colors = assignColors(payments.map((p) => String(p.method)));
  return payments
    .filter((p) => p.amount > 0)
    .map((p) => ({ key: String(p.method), label: PAYMENT_METHOD_LABELS[p.method] ?? 'Other', value: p.amount, color: colors.get(String(p.method)) as string }));
}

export function SalesMixPanel({ params }: { params: RangeParams }) {
  const [view, setView] = useState<MixView>('payment');
  const query = useSalesMix(params);
  const data = query.data;

  const csv = () => {
    if (!data) return null;
    if (view === 'payment')
      return toCsv(['Payment type', 'Amount', 'Payments', 'Credit'], data.payments.map((p) => [PAYMENT_METHOD_LABELS[p.method] ?? 'Other', p.amount, p.paymentCount, p.isCredit ? 'Yes' : 'No']));
    if (view === 'category') return toCsv(['Category', 'Revenue', 'Quantity'], data.categories.map((c) => [c.name, c.revenue, c.quantity]));
    return toCsv(['Hour', 'Sales', 'Transactions', 'Average basket', 'Average per day'], data.hours.map((h) => [hourLabel(h.hour), h.sales, h.transactionCount, h.averageBasket, h.averagePerOperatingDay]));
  };

  return (
    <div className="flex flex-col gap-6">
      <PrintHeader title="Sales mix" params={params} notes={['Gross sales by completion time (UTC+8). Refunds are reported separately.']} />
      <div className="flex flex-wrap items-end justify-between gap-3">
        <SubViewTabs<MixView>
          label="Break sales down by"
          value={view}
          onChange={setView}
          options={[
            { id: 'payment', label: 'Payment type' },
            { id: 'category', label: 'Category' },
            { id: 'hour', label: 'Hour of day' },
          ]}
        />
        <ReportActions filename={`sales-by-${view}`} buildCsv={csv} disabled={!data} />
      </div>

      {data && (
        <div className="grid gap-4 sm:grid-cols-3">
          <Stat label="Gross sales" value={formatPeso(data.grossSales)} />
          <Stat label="Sales rung up" value={String(data.transactionCount)} />
          <Stat label="Trading days" value={String(data.operatingDays)} />
        </div>
      )}

      {view === 'payment' && (
        <AsyncPanel title="Sales by payment type" subtitle="What customers paid with. A split sale counts each part under its own method." query={query} isEmpty={(d) => d.payments.length === 0} emptyMessage="No payments in this period.">
          {(d) => (
            <div className="flex flex-col gap-6">
              <Donut
                ariaLabel="Share of sales by payment type"
                totalLabel="Paid"
                totalValue={formatPeso(d.payments.reduce((s, p) => s + p.amount, 0))}
                formatValue={formatPeso}
                slices={paymentSlices(d.payments)}
              />
              <BarList
                formatValue={formatPeso}
                rows={d.payments.map((p) => ({
                  key: String(p.method),
                  label: PAYMENT_METHOD_LABELS[p.method] ?? 'Other',
                  value: p.amount,
                  detail: `${p.paymentCount} payment${p.paymentCount === 1 ? '' : 's'}${p.isCredit ? ' · credit given, not cash received' : ''}`,
                }))}
              />
              {d.exchangeSettlements > 0 && (
                <p className="text-base text-ink-soft">Exchange price differences collected: {formatPeso(d.exchangeSettlements)} (not included above).</p>
              )}
            </div>
          )}
        </AsyncPanel>
      )}

      {view === 'category' && (
        <AsyncPanel title="Sales by category" subtitle="Follows each item's current category. Open items are shown on their own." query={query} isEmpty={(d) => d.categories.length === 0} emptyMessage="No sales in this period.">
          {(d) => (
            <div className="flex flex-col gap-6">
              <ParetoChart
                rows={paretoRows(d.categories.map((c) => ({ key: c.categoryId ?? c.name, label: c.name, value: c.revenue })))}
                seriesName="Sales"
                formatValue={formatPeso}
                formatAxis={formatPesoCompact}
                ariaLabel="Sales by category, biggest first, with the running share of all sales"
              />
              <BarList
                formatValue={formatPeso}
                rows={d.categories.map((c) => ({ key: c.categoryId ?? c.name, label: c.name, value: c.revenue, detail: `${c.quantity.toLocaleString('en-PH', { maximumFractionDigits: 2 })} sold` }))}
              />
            </div>
          )}
        </AsyncPanel>
      )}

      {view === 'hour' && (
        <>
          <AsyncPanel title="Sales by hour" subtitle="Total sales in each hour (bars) and the average basket (line)" query={query} isEmpty={(d) => d.transactionCount === 0} emptyMessage="No sales in this period.">
            {(d) => (
              <ComboChart
                points={d.hours.map((h) => ({ key: String(h.hour), label: hourLabel(h.hour), bar: h.sales, line: h.averageBasket }))}
                barName="Sales"
                lineName="Average basket"
                formatBar={formatPeso}
                formatLine={formatPeso}
                formatBarAxis={formatPesoCompact}
                formatLineAxis={formatPesoCompact}
                ariaLabel="Sales and average basket for each hour of the day"
              />
            )}
          </AsyncPanel>
          <AsyncPanel title="Sales by weekday" subtitle="Which days of the week carry the sales" query={query} isEmpty={(d) => d.heatmap.length === 0} emptyMessage="Fills in as sales are recorded.">
            {(d) => (
              <StackedColumnChart
                points={DAYS.map((day, i) => ({ key: day, label: day, values: { sales: d.heatmap.filter((c) => c.dayOfWeek === i).reduce((s, c) => s + c.sales, 0) } }))}
                series={[{ key: 'sales', label: 'Sales', color: 'var(--viz-accent)' }]}
                formatValue={formatPeso}
                formatAxis={formatPesoCompact}
                ariaLabel="Total sales for each weekday"
              />
            )}
          </AsyncPanel>
          <AsyncPanel title="Busiest times" subtitle="Sales by weekday and hour. Darker is more." query={query} isEmpty={(d) => d.heatmap.length === 0} emptyMessage="Fills in as sales are recorded.">
            {(d) => <Heatmap cells={d.heatmap} />}
          </AsyncPanel>
        </>
      )}
    </div>
  );
}

function Heatmap({ cells }: { cells: { dayOfWeek: number; hour: number; sales: number }[] }) {
  const max = Math.max(...cells.map((c) => c.sales), 1);
  const byKey = new Map(cells.map((c) => [`${c.dayOfWeek}-${c.hour}`, c.sales]));
  const hours = Array.from(new Set(cells.map((c) => c.hour))).sort((a, b) => a - b);
  const from = hours[0] ?? 0;
  const to = hours[hours.length - 1] ?? 23;
  const range = Array.from({ length: to - from + 1 }, (_, i) => from + i);

  return (
    <div className="overflow-x-auto">
      <table className="border-separate border-spacing-1 text-sm">
        <caption className="sr-only">Sales by weekday and hour</caption>
        <thead>
          <tr>
            <th scope="col" className="sr-only">
              Day
            </th>
            {range.map((h) => (
              <th key={h} scope="col" className="min-w-10 font-medium text-ink-soft">
                {hourLabel(h)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {DAYS.map((day, d) => (
            <tr key={day}>
              <th scope="row" className="pr-2 text-left font-medium text-ink-soft">
                {day}
              </th>
              {range.map((h) => {
                const value = byKey.get(`${d}-${h}`) ?? 0;
                return (
                  <td key={h} title={`${day} ${hourLabel(h)}: ${formatPeso(value)}`} className="h-10 rounded bg-canvas p-0">
                    <div className="h-full w-full rounded bg-brand" style={{ opacity: value === 0 ? 0 : 0.15 + 0.85 * (value / max) }} />
                    <span className="sr-only">{formatPeso(value)}</span>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const TAX_SERIES = [
  { key: 'vatable', label: 'Vatable sales', color: 'var(--viz-1)' },
  { key: 'vat', label: 'VAT', color: 'var(--viz-4)' },
  { key: 'exempt', label: 'VAT-exempt', color: 'var(--viz-6)' },
  { key: 'zero', label: 'Zero-rated', color: 'var(--viz-5)' },
];

const taxValue = (totals: TaxDay, key: string): number =>
  key === 'vatable' ? totals.vatableSales : key === 'vat' ? totals.vatAmount : key === 'exempt' ? totals.vatExemptSales : totals.zeroRatedSales;

export function TaxPanel({ params }: { params: RangeParams }) {
  const query = useTaxReport(params);
  const data = query.data;

  const csv = () =>
    data
      ? toCsv(
          ['Date', 'Sales', 'Gross sales', 'Vatable sales', 'VAT', 'VAT-exempt sales', 'Zero-rated sales'],
          [...data.days, { ...data.totals, date: 'Total' }].map((d: TaxDay) => [d.date, d.saleCount, d.grossSales, d.vatableSales, d.vatAmount, d.vatExemptSales, d.zeroRatedSales]),
        )
      : null;

  return (
    <div className="flex flex-col gap-6">
      <PrintHeader title="Tax report" params={params} notes={['Totals use the VAT amounts stamped on each sale at checkout; they can differ from the X and Z readings by a few centavos.']} />
      <div className="flex justify-end">
        <ReportActions filename="tax-report" buildCsv={csv} disabled={!data} />
      </div>
      {data && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label="Vatable sales" value={formatPeso(data.totals.vatableSales)} />
          <Stat label="VAT" value={formatPeso(data.totals.vatAmount)} />
          <Stat label="VAT-exempt sales" value={formatPeso(data.totals.vatExemptSales)} note={data.seniorPwdSaleCount > 0 ? `${data.seniorPwdSaleCount} Senior/PWD sale${data.seniorPwdSaleCount === 1 ? '' : 's'}` : undefined} />
          <Stat label="Zero-rated sales" value={formatPeso(data.totals.zeroRatedSales)} />
        </div>
      )}
      {data && data.vatDerivedSaleCount > 0 && (
        <p role="note" className="rounded-panel border border-warn bg-surface p-4 text-base">
          {data.vatDerivedSaleCount} sale{data.vatDerivedSaleCount === 1 ? '' : 's'} in this period predate VAT stamping, so their VAT is worked out at 12% rather than recorded at the time of sale.
        </p>
      )}
      {data && data.seniorPwdSaleCount > 0 && (
        <p role="note" className="rounded-panel border border-line bg-surface p-4 text-base text-ink-soft">
          Senior/PWD exempt sales are the sale total plus its discount, which also includes any promo discount on that sale. Confirm with your accountant before filing.
        </p>
      )}
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,19rem)]">
        <AsyncPanel title="What each day's sales were made of" subtitle="Vatable, VAT, exempt and zero-rated, stacked" query={query} isEmpty={(d) => d.days.length === 0} emptyMessage="No sales in this period.">
          {(d) => (
            <StackedColumnChart
              points={d.days.map((day) => ({
                key: day.date,
                label: formatDay(day.date),
                values: { vatable: day.vatableSales, vat: day.vatAmount, exempt: day.vatExemptSales, zero: day.zeroRatedSales },
              }))}
              series={TAX_SERIES}
              formatValue={formatPeso}
              formatAxis={formatPesoCompact}
              ariaLabel="Vatable sales, VAT, exempt sales and zero-rated sales for each day"
            />
          )}
        </AsyncPanel>
        <AsyncPanel title="Split of the period" query={query} isEmpty={(d) => d.totals.grossSales <= 0} emptyMessage="No sales in this period.">
          {(d) => (
            <Donut
              ariaLabel="Share of the period's sales by tax treatment"
              totalLabel="Gross"
              totalValue={formatPeso(d.totals.grossSales)}
              formatValue={formatPeso}
              slices={TAX_SERIES.map((s) => ({ key: s.key, label: s.label, color: s.color, value: taxValue(d.totals, s.key) }))}
            />
          )}
        </AsyncPanel>
      </div>
      <AsyncPanel title="Tax by day" subtitle="Business days in Philippine time" query={query} isEmpty={(d) => d.days.length === 0} emptyMessage="No sales in this period.">
        {(d) => (
          <ReportTable<TaxDay>
            caption="Tax by day"
            rowKey={(r) => r.date}
            rows={d.days}
            columns={[
              { header: 'Day', cell: (r) => formatDay(r.date) },
              { header: 'Sales', numeric: true, cell: (r) => r.saleCount },
              { header: 'Gross', numeric: true, cell: (r) => formatPeso(r.grossSales) },
              { header: 'Vatable', numeric: true, cell: (r) => formatPeso(r.vatableSales) },
              { header: 'VAT', numeric: true, cell: (r) => formatPeso(r.vatAmount) },
              { header: 'Exempt', numeric: true, cell: (r) => formatPeso(r.vatExemptSales) },
              { header: 'Zero-rated', numeric: true, cell: (r) => formatPeso(r.zeroRatedSales) },
            ]}
          />
        )}
      </AsyncPanel>
    </div>
  );
}

const groupColumns = (label: string) => [
  { header: label, cell: (r: ProfitGroup) => r.name },
  { header: 'Revenue', numeric: true, cell: (r: ProfitGroup) => formatPeso(r.knownCostRevenue) },
  { header: 'Cost', numeric: true, cell: (r: ProfitGroup) => formatPeso(r.cost) },
  { header: 'Profit', numeric: true, cell: (r: ProfitGroup) => formatPeso(r.grossProfit) },
  { header: 'Margin', numeric: true, cell: (r: ProfitGroup) => pct(r.marginPercent) },
];

export function ProfitPanel({ params }: { params: RangeParams }) {
  const query = useProfitReport(params);
  const data = query.data;

  const csv = () =>
    data
      ? toCsv(
          ['Group', 'Name', 'Revenue (ex-VAT)', 'Cost', 'Gross profit', 'Margin %', 'Cost coverage %'],
          [
            ...data.categories.map((g) => ['Category', ...groupRow(g)]),
            ...data.topItems.map((g) => ['Top item', ...groupRow(g)]),
            ...data.bottomItems.map((g) => ['Lowest item', ...groupRow(g)]),
          ],
        )
      : null;

  return (
    <div className="flex flex-col gap-6">
      <PrintHeader title="Gross profit and margin" params={params} notes={['Revenue is net of discounts and excludes VAT. Items without a recorded cost are left out of profit and margin.']} />
      <div className="flex justify-end">
        <ReportActions filename="gross-profit" buildCsv={csv} disabled={!data} />
      </div>
      {data && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label="Gross profit" value={formatPeso(data.grossProfit)} note={`On ${formatPeso(data.knownCostRevenue)} of sales with a known cost`} />
          <Stat label="Margin" value={pct(data.marginPercent)} />
          <Stat label="Cost known for" value={pct(data.coveragePercent)} note="Share of sales value" />
          <Stat label="Left out" value={formatPeso(data.unknownCostRevenue)} note="Sold with no cost recorded" />
        </div>
      )}
      {data && (data.coveragePercent ?? 100) < 100 && (
        <p role="note" className="rounded-panel border border-warn bg-surface p-4 text-base">
          Margin only covers items that have a cost price. Add costs in the catalog to include the rest.
        </p>
      )}
      <AsyncPanel title="Profit by day" query={query} isEmpty={(d) => d.days.length === 0} emptyMessage="No sales in this period.">
        {(d) => (
          <AreaLineChart
            points={d.days.map((p) => ({ key: p.date, label: formatDay(p.date), value: p.grossProfit }))}
            seriesName="Gross profit"
            formatValue={formatPeso}
            formatAxis={formatPesoCompact}
            ariaLabel="Gross profit for each day"
          />
        )}
      </AsyncPanel>
      <div className="grid gap-6 lg:grid-cols-2">
        <AsyncPanel title="From sales to profit" subtitle="Revenue with a known cost, less that cost" query={query} isEmpty={(d) => d.knownCostRevenue <= 0} emptyMessage="Profit shows once items with a cost price are sold.">
          {(d) => (
            <WaterfallChart
              steps={[
                { key: 'revenue', label: 'Revenue', value: d.knownCostRevenue, kind: 'total' },
                { key: 'cost', label: 'Cost of goods', value: -d.cost, kind: 'delta' },
                { key: 'profit', label: 'Gross profit', value: d.grossProfit, kind: 'total' },
              ]}
              formatValue={formatPeso}
              formatAxis={formatPesoCompact}
              ariaLabel="Revenue, less cost of goods, leaves gross profit"
            />
          )}
        </AsyncPanel>
        <AsyncPanel title="Category performance" subtitle="Sales against margin. Dashed lines are the middle of your categories." query={query} isEmpty={(d) => d.categories.filter((c) => c.marginPercent !== null).length < 2} emptyMessage="Needs two categories with a recorded cost.">
          {(d) => (
            <ScatterChart
              points={d.categories
                .filter((c) => c.marginPercent !== null)
                .map((c) => ({ key: c.id ?? c.name, label: c.name, x: c.knownCostRevenue, y: c.marginPercent as number }))}
              xName="Sales"
              yName="Margin"
              formatX={formatPesoCompact}
              formatY={(v) => `${v.toFixed(0)}%`}
              ariaLabel="Each category by its sales and its margin"
            />
          )}
        </AsyncPanel>
      </div>
      <AsyncPanel title="Profit by category" query={query} isEmpty={(d) => d.categories.length === 0} emptyMessage="No categorised sales in this period.">
        {(d) => <ReportTable<ProfitGroup> caption="Profit by category" rowKey={(r) => r.id ?? r.name} rows={d.categories} columns={groupColumns('Category')} />}
      </AsyncPanel>
      <div className="grid gap-6 lg:grid-cols-2">
        <AsyncPanel title="Most profitable items" query={query} isEmpty={(d) => d.topItems.length === 0} emptyMessage="No items with a recorded cost were sold.">
          {(d) => <ReportTable<ProfitGroup> caption="Most profitable items" rowKey={(r) => r.id ?? r.name} rows={d.topItems} columns={groupColumns('Item')} />}
        </AsyncPanel>
        <AsyncPanel title="Least profitable items" query={query} isEmpty={(d) => d.bottomItems.length === 0} emptyMessage="Needs more items with a recorded cost.">
          {(d) => <ReportTable<ProfitGroup> caption="Least profitable items" rowKey={(r) => r.id ?? r.name} rows={d.bottomItems} columns={groupColumns('Item')} />}
        </AsyncPanel>
      </div>
    </div>
  );
}

const groupRow = (g: ProfitGroup) => [g.name, g.knownCostRevenue, g.cost, g.grossProfit, g.marginPercent, g.coveragePercent];
