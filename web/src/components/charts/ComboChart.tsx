import { ChartTip, Legend, TableView } from './ChartParts';
import { useChartTip, useElementWidth } from './chartHooks';
import { linear, niceTicks } from './scale';

export interface ComboPoint {
  key: string;
  label: string;
  bar: number;
  line: number;
}

interface ComboChartProps {
  points: ComboPoint[];
  barName: string;
  lineName: string;
  formatBar: (value: number) => string;
  formatLine: (value: number) => string;
  /** Shorter forms for the axis ticks; the tooltip and table keep the full ones. */
  formatBarAxis?: (value: number) => string;
  formatLineAxis?: (value: number) => string;
  ariaLabel: string;
  height?: number;
}

const PAD = { top: 16, right: 64, bottom: 32, left: 56 };

/** Two measures on one time axis: bars on the left scale, a line on the right. Each axis is labelled with its own name. */
export function ComboChart({ points, barName, lineName, formatBar, formatLine, formatBarAxis = formatBar, formatLineAxis = formatLine, ariaLabel, height = 280 }: ComboChartProps) {
  const [ref, width] = useElementWidth<HTMLDivElement>();
  const { tip, show, hide } = useChartTip();
  const barTicks = niceTicks(Math.max(...points.map((p) => p.bar), 0));
  const lineTicks = niceTicks(Math.max(...points.map((p) => p.line), 0));
  const barTop = barTicks[barTicks.length - 1];
  const lineTop = lineTicks[lineTicks.length - 1];
  const plotW = Math.max(60, width - PAD.left - PAD.right);
  const plotH = height - PAD.top - PAD.bottom;
  const yBar = linear([0, barTop], [PAD.top + plotH, PAD.top]);
  const yLine = linear([0, lineTop], [PAD.top + plotH, PAD.top]);
  const slot = plotW / Math.max(1, points.length);
  const barW = Math.max(3, Math.min(28, slot * 0.7));
  const cx = (i: number) => PAD.left + slot * i + slot / 2;
  // An hour with nothing sold has no average, so the line breaks there instead of dropping to zero.
  const path = points
    .map((p, i) => (p.bar <= 0 && p.line <= 0 ? null : `${i > 0 && (points[i - 1].bar > 0 || points[i - 1].line > 0) ? 'L' : 'M'} ${cx(i).toFixed(1)} ${yLine(p.line).toFixed(1)}`))
    .filter((segment) => segment !== null)
    .join(' ');
  const labelEvery = points.length > 12 ? 3 : 1;

  return (
    <figure>
      <div ref={ref} className="relative">
        <svg width={width} height={height} role="img" aria-label={ariaLabel} className="block max-w-full">
          {barTicks.map((t) => (
            <g key={t}>
              <line x1={PAD.left} x2={width - PAD.right} y1={yBar(t)} y2={yBar(t)} stroke="var(--viz-grid)" strokeWidth="1" />
              <text x={PAD.left - 10} y={yBar(t) + 4} textAnchor="end" fontSize="12" fill="var(--ink-soft)">
                {formatBarAxis(t)}
              </text>
            </g>
          ))}
          {lineTicks.map((t) => (
            <text key={t} x={width - PAD.right + 8} y={yLine(t) + 4} fontSize="12" fill="var(--ink-soft)">
              {formatLineAxis(t)}
            </text>
          ))}
          {points.map((p, i) => (
            <g
              key={p.key}
              onPointerEnter={() =>
                show(cx(i), Math.min(yBar(p.bar), yLine(p.line)), {
                  title: p.label,
                  rows: [
                    { label: barName, value: formatBar(p.bar), color: 'var(--viz-accent)' },
                    { label: lineName, value: formatLine(p.line), color: 'var(--ink)' },
                  ],
                })
              }
              onPointerLeave={hide}
            >
              <rect x={cx(i) - slot / 2} y={PAD.top} width={slot} height={plotH} fill="transparent" />
              <rect x={cx(i) - barW / 2} y={yBar(p.bar)} width={barW} height={Math.max(0, PAD.top + plotH - yBar(p.bar))} rx="3" fill="var(--viz-accent)" fillOpacity="0.85" />
              {i % labelEvery === 0 && (
                <text x={cx(i)} y={height - 8} textAnchor="middle" fontSize="12" fill="var(--ink-soft)">
                  {p.label}
                </text>
              )}
            </g>
          ))}
          {points.length > 1 && <path d={path} fill="none" stroke="var(--ink)" strokeWidth="2" strokeLinejoin="round" pointerEvents="none" />}
        </svg>
        <ChartTip tip={tip} width={width} />
      </div>
      <Legend
        className="mt-3 sm:flex-row sm:flex-wrap sm:gap-x-6"
        items={[
          { key: 'bar', label: `${barName} (left axis)`, color: 'var(--viz-accent)' },
          { key: 'line', label: `${lineName} (right axis)`, color: 'var(--ink)' },
        ]}
      />
      <TableView
        caption={ariaLabel}
        columns={[{ label: 'Hour' }, { label: barName, align: 'right' }, { label: lineName, align: 'right' }]}
        rows={points.map((p) => [p.label, formatBar(p.bar), formatLine(p.line)])}
      />
    </figure>
  );
}
