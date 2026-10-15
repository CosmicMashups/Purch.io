import { Warning } from '@phosphor-icons/react';
import { useState } from 'react';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { formatPeso } from '../../dashboard/format';
import { toCsv } from '../csv';
import { useInventoryValuation } from '../queries';
import type { ValuationRow } from '../types';
import { ReportActions, ReportTable, Stat, SubViewTabs } from './ReportParts';

const KINDS = [
  { id: 'all', label: 'All stock' },
  { id: 'item', label: 'Items' },
  { id: 'ingredient', label: 'Ingredients' },
];
const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;
const flag = (r: ValuationRow) => (r.value === null ? 'Unknown cost' : r.flagged ? 'Negative stock' : '');

export function ValuationPanel() {
  const [type, setType] = useState('all');
  const query = useInventoryValuation(type);
  const d = query.data;
  const unknown = (d?.groups.flatMap((g) => g.rows) ?? []).filter((r) => r.value === null);

  const csv = () =>
    d
      ? toCsv(
          ['Category', 'Name', 'Kind', 'SKU', 'On hand', 'Unit cost', 'Value', 'Flag'],
          d.groups.flatMap((g) => g.rows.map((r) => [g.name, r.name, r.kind, r.sku, r.onHand, r.unitCost, r.value, flag(r)])),
        )
      : null;

  return (
    <div className="flex flex-col gap-6">
      <AsyncPanel
        title="Stock valuation"
        subtitle="On-hand quantity at current cost."
        query={query}
        isEmpty={(x) => x.groups.length === 0}
        emptyMessage="There is no stock to value yet."
        tabs={<SubViewTabs label="Stock kind" value={type} options={KINDS} onChange={setType} />}
      >
        {(x) => (
          <div className="flex flex-col gap-4">
            {x.unknownCostCount > 0 && (
              <p role="status" className="flex items-start gap-2 rounded-panel border border-warn bg-warn/5 p-4 text-base text-ink">
                <Warning size={24} weight="fill" className="shrink-0 text-warn" aria-hidden="true" />
                <span>Unknown cost excluded from the total: {plural(x.unknownCostCount, 'item has', 'items have')} no cost recorded.</span>
              </p>
            )}
            <div className="grid gap-3 sm:grid-cols-2">
              <Stat label="Total stock value" value={formatPeso(x.totalValue)} note={`${plural(x.valuedCount, 'item', 'items')} valued`} />
              <Stat
                label="Items without a cost"
                value={String(x.unknownCostCount)}
                note="left out of the value"
              />
            </div>
            {x.negativeStockCount > 0 && (
              <p role="status" className="flex items-start gap-2 rounded-panel border border-warn bg-warn/5 p-4 text-base text-ink">
                <Warning size={24} weight="fill" className="shrink-0 text-warn" aria-hidden="true" />
                <span>{plural(x.negativeStockCount, 'item has negative stock', 'items have negative stock')}, valued at 0 and flagged below.</span>
              </p>
            )}
            {unknown.length > 0 && (
              <details className="rounded-panel border border-line bg-surface p-4">
                <summary className="flex min-h-12 cursor-pointer items-center text-base font-semibold">Show the {unknown.length} without a cost</summary>
                <ul className="mt-2 list-disc pl-5 text-base">
                  {unknown.map((r) => (
                    <li key={r.id}>{r.name}</li>
                  ))}
                </ul>
              </details>
            )}
            {x.groups.map((g) => (
              <section key={g.categoryId ?? g.name} className="flex flex-col gap-2">
                <h3 className="text-lg font-bold">
                  {g.name} <span className="font-normal text-ink-soft">{formatPeso(g.subtotal)}</span>
                </h3>
                <ReportTable<ValuationRow>
                  caption={`${g.name} stock valuation`}
                  rows={g.rows}
                  rowKey={(r) => `${r.kind}-${r.id}`}
                  columns={[
                    { header: 'Name', cell: (r) => r.name },
                    { header: 'On hand', numeric: true, cell: (r) => r.onHand },
                    { header: 'Unit cost', numeric: true, cell: (r) => (r.unitCost === null ? 'Unknown' : formatPeso(r.unitCost)) },
                    { header: 'Value', numeric: true, cell: (r) => (r.value === null ? 'Unknown' : formatPeso(r.value)) },
                    { header: 'Flag', cell: flag },
                  ]}
                />
              </section>
            ))}
          </div>
        )}
      </AsyncPanel>
      <ReportActions filename="stock-valuation" buildCsv={csv} disabled={!d} />
    </div>
  );
}
