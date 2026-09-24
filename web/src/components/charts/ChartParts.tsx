import type { TipState } from './chartHooks';

/** The readout: values lead, names follow. Placed above the point and kept inside the chart's width. */
export function ChartTip({ tip, width }: { tip: TipState | null; width: number }) {
  if (!tip) return null;
  const x = Math.min(Math.max(tip.x, 72), Math.max(72, width - 72));
  const below = tip.y < 76;
  return (
    <div
      role="tooltip"
      className="pointer-events-none absolute z-10 w-max max-w-56 rounded-control border border-line bg-surface px-3 py-2 shadow-md shadow-ink/10"
      style={{ left: x, top: tip.y, transform: below ? 'translate(-50%, 12px)' : 'translate(-50%, calc(-100% - 12px))' }}
    >
      <p className="text-xs text-ink-soft">{tip.title}</p>
      <ul className="mt-1 flex flex-col gap-0.5">
        {tip.rows.map((row) => (
          <li key={row.label} className="flex items-center gap-2 text-sm">
            {row.color && <span aria-hidden="true" className="h-0.5 w-3 shrink-0 rounded-full" style={{ background: row.color }} />}
            <span className="font-semibold tabular-nums">{row.value}</span>
            <span className="text-ink-soft">{row.label}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

export interface LegendItem {
  key: string;
  label: string;
  color: string;
  /** Text shown after the name, such as an amount and a share. */
  detail?: string;
  hollow?: boolean;
}

/** A legend is always present for two or more series, and it carries the values so colour is never the only channel. */
export function Legend({ items, className = '' }: { items: LegendItem[]; className?: string }) {
  return (
    <ul className={`flex flex-col gap-2 ${className}`}>
      {items.map((item) => (
        <li key={item.key} className="flex items-center gap-3 text-sm">
          <span
            aria-hidden="true"
            className="size-3 shrink-0 rounded-[4px]"
            style={item.hollow ? { border: `2px solid ${item.color}` } : { background: item.color }}
          />
          <span className="min-w-0 flex-1 truncate">{item.label}</span>
          {item.detail && <span className="shrink-0 tabular-nums text-ink-soft">{item.detail}</span>}
        </li>
      ))}
    </ul>
  );
}

export interface TableColumn {
  label: string;
  align?: 'left' | 'right';
}

/** Every chart's numbers, as plain text, one tap away. Tooltips enhance; this never depends on hover. */
export function TableView({ caption, columns, rows }: { caption: string; columns: TableColumn[]; rows: string[][] }) {
  return (
    <details className="group mt-3">
      <summary className="inline-flex h-12 cursor-pointer items-center text-sm font-semibold text-brand-strong underline">
        Show as table
      </summary>
      <div className="overflow-x-auto">
        <table className="mt-1 w-full text-sm">
          <caption className="sr-only">{caption}</caption>
          <thead>
            <tr className="border-b border-line text-ink-soft">
              {columns.map((c) => (
                <th key={c.label} scope="col" className={`py-2 font-medium ${c.align === 'right' ? 'text-right' : 'text-left'}`}>
                  {c.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => (
              <tr key={i} className="border-b border-line/60 last:border-0">
                {row.map((cell, j) => (
                  <td key={j} className={`py-2 tabular-nums ${columns[j]?.align === 'right' ? 'text-right' : 'text-left'}`}>
                    {cell}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
}
