import { useState } from 'react';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { formatPeso } from '../../dashboard/format';
import { toCsv } from '../csv';
import { useInventoryCogs } from '../queries';
import type { CogsGroup, CogsGroupBy, RangeParams } from '../types';
import { PrintHeader, ReportActions, ReportTable, Stat, SubViewTabs } from './ReportParts';

const VIEWS: { id: CogsGroupBy; label: string }[] = [
  { id: 'item', label: 'By item' },
  { id: 'category', label: 'By category' },
  { id: 'day', label: 'By day' },
];

export function CogsPanel({ params }: { params: RangeParams }) {
  const [groupBy, setGroupBy] = useState<CogsGroupBy>('item');
  const query = useInventoryCogs(params, groupBy);
  const d = query.data;

  const csv = () =>
    d
      ? toCsv(
          ['Name', 'Quantity', 'COGS', 'Known-cost revenue', 'Coverage %', 'Excluded lines'],
          d.groups.map((g) => [g.name, g.quantity, g.cogs, g.knownCostRevenue, g.coveragePercent, g.excludedLineCount]),
        )
      : null;

  return (
    <div className="flex flex-col gap-6">
      <PrintHeader title="Cost of goods sold" params={params} />
      <AsyncPanel
        title="Cost of goods sold"
        query={query}
        isEmpty={(x) => x.groups.length === 0}
        emptyMessage="No sales in this period."
        tabs={<SubViewTabs label="Group COGS" value={groupBy} options={VIEWS} onChange={setGroupBy} />}
      >
        {(x) => (
          <div className="flex flex-col gap-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <Stat label="COGS" value={formatPeso(x.cogs)} />
              <Stat label="Revenue with a known cost" value={formatPeso(x.knownCostRevenue)} />
              <Stat label="Cost coverage" value={`${x.coveragePercent.toFixed(1)}%`} />
            </div>
            <p className="rounded-panel border border-line bg-surface p-4 text-base text-ink-soft">
              {x.excludedLineCount} {x.excludedLineCount === 1 ? 'line has' : 'lines have'} no recorded cost and{' '}
              {x.excludedLineCount === 1 ? 'is' : 'are'} left out. Recipe use is not included in COGS, only each finished item's own cost. Quantities are net
              of refunds, and refunds net against the original sale date.
            </p>
            <ReportTable<CogsGroup>
              caption="COGS"
              rows={x.groups}
              rowKey={(g) => g.id ?? g.name}
              columns={[
                { header: 'Name', cell: (g) => g.name },
                { header: 'Qty', numeric: true, cell: (g) => g.quantity },
                { header: 'COGS', numeric: true, cell: (g) => formatPeso(g.cogs) },
                { header: 'Coverage', numeric: true, cell: (g) => `${g.coveragePercent.toFixed(1)}%` },
              ]}
            />
          </div>
        )}
      </AsyncPanel>
      <ReportActions filename="cogs" buildCsv={csv} disabled={!d} />
    </div>
  );
}
