export interface RankedRow {
  key: string;
  label: string;
  value: number;
  /** Text after the value, such as a count of sales. */
  detail?: string;
}

/**
 * Wide containers put label, chart and value on one line. Narrow ones (a half-width panel) keep the label and value on
 * the first line and give the chart the whole width beneath, so bars never get squeezed by a fixed label column.
 */
const GRID = 'grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-1 @xl:grid-cols-[minmax(0,11rem)_minmax(0,1fr)_auto]';
const LABEL = '@xl:col-start-1 @xl:row-start-1';
const CHART = 'col-span-2 row-start-2 @xl:col-span-1 @xl:col-start-2 @xl:row-start-1';
const VALUE = 'justify-end @xl:col-start-3 @xl:row-start-1';

function Label({ row }: { row: RankedRow }) {
  return (
    <span className={`min-w-0 truncate text-base font-medium ${LABEL}`} title={row.label}>
      {row.label}
    </span>
  );
}

function Value({ row, formatValue }: { row: RankedRow; formatValue: (v: number) => string }) {
  return (
    <span className={`flex items-baseline gap-2 ${VALUE}`}>
      <span className="text-sm font-semibold tabular-nums">{formatValue(row.value)}</span>
      {row.detail && <span className="text-xs text-ink-soft">{row.detail}</span>}
    </span>
  );
}

const maxOf = (rows: RankedRow[]) => Math.max(...rows.map((r) => r.value), 0);
const pct = (value: number, max: number) => (max > 0 ? Math.max(0, Math.min(100, (value / max) * 100)) : 0);

/** Magnitude, largest first: thin bars from a single baseline, square there and rounded at the data end. */
export function BarList({ rows, formatValue }: { rows: RankedRow[]; formatValue: (v: number) => string }) {
  const max = maxOf(rows);
  return (
    <ol className="@container flex flex-col gap-3">
      {rows.map((row) => (
        <li key={row.key} className={GRID}>
          <Label row={row} />
          <div className={`h-3.5 border-l border-viz-muted ${CHART}`}>
            <div className="h-full rounded-r-[4px]" style={{ width: `${pct(row.value, max)}%`, minWidth: row.value > 0 ? 3 : 0, background: 'var(--viz-accent)' }} />
          </div>
          <Value row={row} formatValue={formatValue} />
        </li>
      ))}
    </ol>
  );
}

/** Magnitude as a stem and a dot. Lighter than a bar, so it suits a ranking of people. */
export function LollipopList({ rows, formatValue }: { rows: RankedRow[]; formatValue: (v: number) => string }) {
  const max = maxOf(rows);
  return (
    <ol className="@container flex flex-col gap-3">
      {rows.map((row) => {
        const at = pct(row.value, max) * 0.94;
        return (
          <li key={row.key} className={GRID}>
            <Label row={row} />
            <div className={`relative h-6 ${CHART}`} aria-hidden="true">
              <div className="absolute left-0 top-1/2 h-0.5 -translate-y-1/2 rounded-full" style={{ width: `${at}%`, background: 'var(--viz-accent)' }} />
              <div className="absolute top-1/2 size-3.5 -translate-x-1/2 -translate-y-1/2 rounded-full ring-2 ring-surface" style={{ left: `${at}%`, background: 'var(--viz-accent)' }} />
            </div>
            <Value row={row} formatValue={formatValue} />
          </li>
        );
      })}
    </ol>
  );
}

/** Values on one shared scale as dots against a faint guide, so the reader compares positions, not lengths. */
export function DotPlot({ rows, formatValue }: { rows: RankedRow[]; formatValue: (v: number) => string }) {
  const max = maxOf(rows);
  return (
    <ol className="@container flex flex-col gap-3">
      {rows.map((row) => {
        const at = pct(row.value, max) * 0.96;
        return (
          <li key={row.key} className={GRID}>
            <Label row={row} />
            <div className={`relative h-6 ${CHART}`} aria-hidden="true">
              <div className="absolute inset-x-0 top-1/2 h-px -translate-y-1/2 bg-viz-grid" />
              <div className="absolute top-1/2 size-3.5 -translate-x-1/2 -translate-y-1/2 rounded-full ring-2 ring-surface" style={{ left: `${at}%`, background: 'var(--viz-accent)' }} />
            </div>
            <Value row={row} formatValue={formatValue} />
          </li>
        );
      })}
    </ol>
  );
}

export interface BulletRow {
  key: string;
  label: string;
  value: number;
  target: number;
}

interface BulletListProps {
  rows: BulletRow[];
  /**
   * 'floor': the target is a minimum (stock against its alert level), so being at or under it is the problem.
   * 'ceiling': the target is a limit (credit against its cap), so nearing or passing it is the problem.
   */
  mode: 'floor' | 'ceiling';
  /** Text at the end of each row. `alert` makes the value stand out as the problem. */
  describe: (row: BulletRow) => { value: string; note: string; alert?: boolean };
}

/** The share of a limit after which the bar warns. */
const NEAR_LIMIT = 0.8;

/**
 * A measure against a target. The bar is the measure, the pale band is the zone the target guards, and the tick is
 * the target itself. The bar takes a warning colour when it is on the wrong side of the target.
 */
export function BulletList({ rows, mode, describe }: BulletListProps) {
  const scale = Math.max(...rows.flatMap((r) => [r.value, r.target]), 1) * 1.1;
  return (
    <ol className="@container flex flex-col gap-4">
      {rows.map((row) => {
        const text = describe(row);
        const warn = mode === 'floor' ? row.value <= row.target : row.value >= row.target * NEAR_LIMIT;
        const over = mode === 'ceiling' && row.value > row.target;
        const bar = over ? 'var(--color-danger)' : warn ? 'var(--color-warn)' : 'var(--viz-accent)';
        const band = mode === 'floor' ? 'color-mix(in srgb, var(--color-warn) 16%, var(--surface))' : 'color-mix(in srgb, var(--viz-accent) 12%, var(--surface))';
        return (
          <li key={row.key} className={GRID}>
            <span className={`min-w-0 truncate text-base font-medium ${LABEL}`} title={row.label}>
              {row.label}
            </span>
            <div className={`relative h-4 ${CHART}`} role="img" aria-label={`${row.label}: ${text.value} ${text.note}`}>
              <div className="absolute inset-y-0 left-0 rounded-[4px]" style={{ width: `${(row.target / scale) * 100}%`, background: band }} />
              <div
                className="absolute left-0 top-1/2 h-2 -translate-y-1/2 rounded-r-[4px]"
                style={{ width: `${(Math.max(row.value, 0) / scale) * 100}%`, minWidth: row.value > 0 ? 3 : 0, background: bar }}
              />
              <div className="absolute inset-y-0 w-0.5 rounded-full bg-ink" style={{ left: `${(row.target / scale) * 100}%` }} />
            </div>
            <span className={`text-sm tabular-nums text-right ${"@xl:col-start-3 @xl:row-start-1"}`}>
              <span className={`font-semibold ${text.alert ? 'text-danger' : ''}`}>{text.value}</span>
              <span className="text-ink-soft">{` ${text.note}`}</span>
            </span>
          </li>
        );
      })}
    </ol>
  );
}
