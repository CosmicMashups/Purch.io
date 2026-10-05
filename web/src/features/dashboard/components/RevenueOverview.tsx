import type { RangeParams } from '../../reports/types';
import { AreaLineChart } from '../../../components/charts/AreaLineChart';
import { formatDay, formatPeso, formatPesoCompact } from '../format';
import { useSalesDashboard } from '../queries';
import { AsyncPanel } from './AsyncPanel';

/** Today's takings beside the shape of the last month. Every figure is the server's; the chart only draws them. */
export function RevenueOverview({ range, periodLabel }: { range: RangeParams | null; periodLabel: string }) {
  const sales = useSalesDashboard(range !== null, range);
  return (
    <AsyncPanel
      title="Revenue"
      subtitle="What the business has taken, day by day"
      query={sales}
      isEmpty={(d) => d.trend.length === 0 && d.revenueInRange === 0 && d.revenueToday === 0}
      emptyMessage="No sales recorded yet. Completed sales appear here after checkout."
      minHeight="min-h-56"
    >
      {(d) => (
        <div className="grid gap-x-10 gap-y-6 lg:grid-cols-[13rem_minmax(0,1fr)]">
          <div className="flex flex-col justify-between gap-6">
            <div>
              <p className="text-sm font-medium text-ink-soft">Today</p>
              <p className="mt-1 text-5xl font-bold tracking-tight">{formatPeso(d.revenueToday)}</p>
            </div>
            <dl className="flex gap-8 lg:flex-col lg:gap-4">
              <div>
                <dt className="text-sm text-ink-soft">{periodLabel}</dt>
                <dd className="text-xl font-semibold tabular-nums">{formatPeso(d.revenueInRange)}</dd>
              </div>
            </dl>
          </div>
          {d.trend.length > 0 ? (
            <AreaLineChart
              points={d.trend.map((p) => ({ key: p.date, label: formatDay(p.date), value: p.revenue }))}
              seriesName="Revenue"
              formatValue={formatPeso}
              formatAxis={formatPesoCompact}
              ariaLabel={`Daily revenue, ${formatDay(d.trend[0].date)} to ${formatDay(d.trend[d.trend.length - 1].date)}`}
            />
          ) : (
            <p className="flex items-center text-base text-ink-soft">No daily figures to chart yet.</p>
          )}
        </div>
      )}
    </AsyncPanel>
  );
}
