import { ChartTip, Legend, TableView } from './ChartParts';
import { useChartTip, useElementWidth } from './chartHooks';
import { linear, niceTicks } from './scale';

export interface StackSeries {
  key: string;
  label: string;
  color: string;
}

export interface StackPoint {
  key: string;
  label: string;
  values: Record<string, number>;
}

interface StackedColumnChartProps {
  points: StackPoint[];
  series: StackSeries[];
  formatValue: (value: number) => string;
  formatAxis: (value: number) => string;
  ariaLabel: string;
  height?: number;
}

const PAD = { top: 16, right: 16, bottom: 32, left: 56 };

/** Parts of each day's total, stacked, so both the size of the day and what it was made of are visible. */
export function StackedColumnChart({ points, series, formatValue, formatAxis, ariaLabel, height = 260 }: StackedColumnChartProps) {
  const [ref, width] = useElementWidth<HTMLDivElement>();
  const { tip, show, hide } = useChartTip();
  const totals = points.map((p) => series.reduce((sum, s) => sum + Math.max(0, p.values[s.key] ?? 0), 0));
  const max = Math.max(...totals, 0);
  const ticks = niceTicks(max);
  const top = ticks[ticks.length - 1];
  const plotW = Math.max(60, width - PAD.left - PAD.right);
  const plotH = height - PAD.top - PAD.bottom;
  const y = linear([0, top], [PAD.top + plotH, PAD.top]);
  const slot = plotW / Math.max(1, points.length);
  const barW = Math.max(4, Math.min(44, slot * 0.72));
  const labelAt = points.length <= 10 ? points.map((_, i) => i) : [...new Set([0, Math.floor((points.length - 1) / 2), points.length - 1])];

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
          {points.map((point, i) => {
            const cx = PAD.left + slot * i + slot / 2;
            let base = 0;
            return (
              <g
                key={point.key}
                onPointerEnter={() =>
                  show(cx, y(totals[i]), {
                    title: point.label,
                    rows: [
                      ...series.map((s) => ({ label: s.label, value: formatValue(point.values[s.key] ?? 0), color: s.color })),
                      { label: 'total', value: formatValue(totals[i]) },
                    ],
                  })
                }
                onPointerLeave={hide}
              >
                <rect x={cx - slot / 2} y={PAD.top} width={slot} height={plotH} fill="transparent" />
                {series.map((s) => {
                  const v = Math.max(0, point.values[s.key] ?? 0);
                  if (v <= 0) return null;
                  const from = base;
                  base += v;
                  return <rect key={s.key} x={cx - barW / 2} y={y(base)} width={barW} height={Math.max(1, y(from) - y(base) - 1)} rx="2" fill={s.color} />;
                })}
              </g>
            );
          })}
          {labelAt.map((i) => (
            <text
              key={i}
              x={PAD.left + slot * i + slot / 2}
              y={height - 8}
              textAnchor={points.length > 10 && i === 0 ? 'start' : points.length > 10 && i === points.length - 1 ? 'end' : 'middle'}
              fontSize="12"
              fill="var(--ink-soft)"
            >
              {points[i]?.label}
            </text>
          ))}
        </svg>
        <ChartTip tip={tip} width={width} />
      </div>
      <Legend
        className="mt-3 sm:flex-row sm:flex-wrap sm:gap-x-6"
        items={series.map((s) => ({ key: s.key, label: s.label, color: s.color }))}
      />
      <TableView
        caption={ariaLabel}
        columns={[{ label: 'Day' }, ...series.map((s) => ({ label: s.label, align: 'right' as const }))]}
        rows={points.map((p) => [p.label, ...series.map((s) => formatValue(p.values[s.key] ?? 0))])}
      />
    </figure>
  );
}
