import { ChartTip, TableView } from './ChartParts';
import { useChartTip, useElementWidth } from './chartHooks';
import type { ParetoRow } from './chartData';
import { linear, niceTicks } from './scale';

interface ParetoChartProps {
  rows: ParetoRow[];
  seriesName: string;
  formatValue: (value: number) => string;
  formatAxis: (value: number) => string;
  ariaLabel: string;
  height?: number;
}

const PAD = { top: 20, right: 48, bottom: 56, left: 56 };

/** Bars biggest first with the running share on top, so "how few of these make most of it" reads at a glance. */
export function ParetoChart({ rows, seriesName, formatValue, formatAxis, ariaLabel, height = 300 }: ParetoChartProps) {
  const [ref, width] = useElementWidth<HTMLDivElement>();
  const { tip, show, hide } = useChartTip();
  const max = Math.max(...rows.map((r) => r.value), 0);
  const ticks = niceTicks(max);
  const top = ticks[ticks.length - 1];
  const plotW = Math.max(60, width - PAD.left - PAD.right);
  const plotH = height - PAD.top - PAD.bottom;
  const y = linear([0, top], [PAD.top + plotH, PAD.top]);
  const yPct = linear([0, 100], [PAD.top + plotH, PAD.top]);
  const slot = plotW / Math.max(1, rows.length);
  const barW = Math.min(56, slot * 0.64);
  const cx = (i: number) => PAD.left + slot * i + slot / 2;
  const line = rows.map((r, i) => `${i === 0 ? 'M' : 'L'} ${cx(i).toFixed(1)} ${yPct(r.cumulativePercent).toFixed(1)}`).join(' ');

  return (
    <figure>
      <div ref={ref} className="relative">
        <svg width={width} height={height} role="img" aria-label={ariaLabel} className="block max-w-full">
          {ticks.map((t) => (
            <g key={t}>
              <line x1={PAD.left} x2={width - PAD.right} y1={y(t)} y2={y(t)} stroke="var(--viz-grid)" strokeWidth="1" />
              <text x={PAD.left - 10} y={y(t) + 4} textAnchor="end" fontSize="12" fill="var(--ink-soft)">
                {formatAxis(t)}
              </text>
            </g>
          ))}
          {[0, 50, 100].map((p) => (
            <text key={p} x={width - PAD.right + 8} y={yPct(p) + 4} fontSize="12" fill="var(--ink-soft)">{`${p}%`}</text>
          ))}
          <line x1={PAD.left} x2={width - PAD.right} y1={yPct(80)} y2={yPct(80)} stroke="var(--ink-soft)" strokeDasharray="4 4" strokeOpacity="0.6" />
          <text x={PAD.left + 4} y={yPct(80) - 6} fontSize="12" fill="var(--ink-soft)">
            80% of the total
          </text>

          {rows.map((row, i) => (
            <g key={row.key}>
              <rect
                x={cx(i) - barW / 2}
                y={y(row.value)}
                width={barW}
                height={Math.max(2, PAD.top + plotH - y(row.value))}
                rx="4"
                fill={row.key === 'other' ? 'var(--viz-muted)' : 'var(--viz-accent)'}
                onPointerEnter={() =>
                  show(cx(i), y(row.value), {
                    title: row.label,
                    rows: [
                      { label: seriesName, value: formatValue(row.value) },
                      { label: 'running share', value: `${row.cumulativePercent}%` },
                    ],
                  })
                }
                onPointerLeave={hide}
              />
              <text x={cx(i)} y={height - PAD.bottom + 18} textAnchor="middle" fontSize="12" fill="var(--ink-soft)">
                {row.label.length > 12 ? `${row.label.slice(0, 11)}...` : row.label}
              </text>
            </g>
          ))}
          {rows.length > 1 && <path d={line} fill="none" stroke="var(--ink)" strokeWidth="2" strokeLinejoin="round" />}
          {rows.map((row, i) => (
            <circle key={row.key} cx={cx(i)} cy={yPct(row.cumulativePercent)} r="4" fill="var(--ink)" stroke="var(--surface)" strokeWidth="2" pointerEvents="none" />
          ))}
        </svg>
        <ChartTip tip={tip} width={width} />
      </div>
      <TableView
        caption={ariaLabel}
        columns={[{ label: 'Name' }, { label: seriesName, align: 'right' }, { label: 'Running share', align: 'right' }]}
        rows={rows.map((r) => [r.label, formatValue(r.value), `${r.cumulativePercent}%`])}
      />
    </figure>
  );
}
