import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { AreaLineChart } from '../../../components/charts/AreaLineChart';
import { BarList } from '../../../components/charts/RankedCharts';
import { useDashboardInsights } from '../../reports/queries';
import type { RangeParams } from '../../reports/types';
import { formatDay, formatPeso } from '../format';
import { AsyncPanel } from './AsyncPanel';

const pct = (v: number | null) => (v === null ? 'n/a' : `${v.toFixed(1)}%`);

/** True once the element has come within a screenful of the viewport. The insights call waits for this, so
 * the dashboard's first paint and its data budget never include numbers nobody has scrolled to. */
function useNearViewport<T extends Element>() {
  const ref = useRef<T>(null);
  const [near, setNear] = useState(typeof IntersectionObserver === 'undefined');
  useEffect(() => {
    const el = ref.current;
    if (!el || near) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          setNear(true);
          observer.disconnect();
        }
      },
      { rootMargin: '400px' },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [near]);
  return [ref, near] as const;
}

/** The three figures an owner checks to know whether the business is healthy: margin, leakage, and what earns it. */
export function ExecutiveInsights({ range, periodLabel }: { range: RangeParams | null; periodLabel: string }) {
  const [ref, near] = useNearViewport<HTMLDivElement>();
  const insights = useDashboardInsights(range, near);

  return (
    <section ref={ref} aria-labelledby="insights-heading" className="flex flex-col gap-6">
      <div className="flex flex-wrap items-baseline justify-between gap-3">
        <h2 id="insights-heading" className="text-2xl font-bold tracking-tight">
          Profit and leakage
        </h2>
        <Link to="/business/reports?tab=profit" className="inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
          Full profit report
        </Link>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <AsyncPanel
          title="Gross margin"
          subtitle={`Profit each day · ${periodLabel}${insights.data ? ` · margin ${pct(insights.data.marginPercent)}, cost known for ${pct(insights.data.coveragePercent)} of sales` : ''}`}
          query={insights}
          isEmpty={(d) => d.marginTrend.length === 0}
          emptyMessage="Profit shows once items with a cost price are sold."
        >
          {(d) => (
            <AreaLineChart
              points={d.marginTrend.map((p) => ({ key: p.date, label: formatDay(p.date), value: p.grossProfit }))}
              seriesName="Gross profit"
              formatValue={formatPeso}
              formatAxis={formatPeso}
              ariaLabel="Gross profit for each day"
            />
          )}
        </AsyncPanel>

        <AsyncPanel
          title="Sales leakage"
          subtitle={`Discounts, voids and refunds as a share of sales${insights.data ? ` · ${insights.data.leakagePercent.toFixed(1)}% overall` : ''}`}
          query={insights}
          isEmpty={(d) => d.leakage.length === 0}
          emptyMessage="No sales in this period."
        >
          {(d) => (
            <AreaLineChart
              points={d.leakage.map((p) => ({ key: p.date, label: formatDay(p.date), value: p.leakagePercent }))}
              seriesName="Leakage"
              formatValue={(v) => `${v.toFixed(1)}%`}
              formatAxis={(v) => `${v.toFixed(0)}%`}
              ariaLabel="Share of sales lost to discounts, voids and refunds, per day"
            />
          )}
        </AsyncPanel>
      </div>

      <AsyncPanel
        title="What earns the money"
        subtitle="Category share of sales, with margin where cost is known"
        query={insights}
        isEmpty={(d) => d.categories.length === 0}
        emptyMessage="No sales in this period."
      >
        {(d) => (
          <BarList
            formatValue={formatPeso}
            rows={d.categories.map((c) => ({
              key: c.name,
              label: c.name,
              value: c.revenue,
              detail: `${c.sharePercent.toFixed(1)}% of sales${c.isOpenItem ? '' : ` · margin ${pct(c.marginPercent)}`}`,
            }))}
          />
        )}
      </AsyncPanel>
    </section>
  );
}
