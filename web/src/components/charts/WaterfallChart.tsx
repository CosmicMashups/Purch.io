import { ChartTip, TableView } from './ChartParts';
import { useChartTip, useElementWidth } from './chartHooks';
import { waterfallBars, type WaterfallStep } from './chartData';
import { linear, niceTicks } from './scale';

interface WaterfallChartProps {
  steps: WaterfallStep[];
  formatValue: (value: number) => string;
  formatAxis: (value: number) => string;
  ariaLabel: string;
  height?: number;
}

const PAD = { top: 28, right: 16, bottom: 52, left: 56 };

/** How a total is built up or taken down: totals stand on the baseline, deltas hang from the running figure. */
export function WaterfallChart({ steps, formatValue, formatAxis, ariaLabel, height = 280 }: WaterfallChartProps) {
  const [ref, width] = useElementWidth<HTMLDivElement>();
  const { tip, show, hide } = useChartTip();
  const bars = waterfallBars(steps);
  const max = Math.max(...bars.flatMap((b) => [b.from, b.to]), 0);
  const ticks = niceTicks(max);
  const top = ticks[ticks.length - 1];
  const plotW = Math.max(60, width - PAD.left - PAD.right);
  const plotH = height - PAD.top - PAD.bottom;
  const y = linear([0, top], [PAD.top + plotH, PAD.top]);
  const slot = plotW / Math.max(1, bars.length);
  const barW = Math.min(64, slot * 0.6);

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
          {bars.map((bar, i) => {
            const cx = PAD.left + slot * i + slot / 2;
            const hi = Math.max(bar.from, bar.to);
            const lo = Math.min(bar.from, bar.to);
            const color = bar.kind === 'total' ? 'var(--viz-accent)' : bar.value < 0 ? 'var(--viz-8)' : 'var(--viz-6)';
            const next = bars[i + 1];
            return (
              <g key={bar.key}>
                {next && <line x1={cx + barW / 2} x2={cx + slot - barW / 2} y1={y(bar.to)} y2={y(bar.to)} stroke="var(--ink-soft)" strokeOpacity="0.5" strokeDasharray="3 3" />}
                <rect
                  x={cx - barW / 2}
                  y={y(hi)}
                  width={barW}
                  height={Math.max(2, y(lo) - y(hi))}
                  rx="4"
                  fill={color}
                  onPointerEnter={() =>
                    show(cx, y(hi), {
                      title: bar.label,
                      rows: [{ label: bar.kind === 'total' ? 'Total' : bar.value < 0 ? 'Less' : 'Add', value: formatValue(Math.abs(bar.value)), color }],
                    })
                  }
                  onPointerLeave={hide}
                />
                <text x={cx} y={y(hi) - 8} textAnchor="middle" fontSize="12" fontWeight="600" fill="var(--ink)">
                  {bar.kind === 'delta' && bar.value < 0 ? `-${formatValue(Math.abs(bar.value))}` : formatValue(bar.value)}
                </text>
                <text x={cx} y={height - PAD.bottom + 18} textAnchor="middle" fontSize="12" fill="var(--ink-soft)">
                  {bar.label.length > 16 ? `${bar.label.slice(0, 15)}...` : bar.label}
                </text>
              </g>
            );
          })}
        </svg>
        <ChartTip tip={tip} width={width} />
      </div>
      <TableView
        caption={ariaLabel}
        columns={[{ label: 'Step' }, { label: 'Amount', align: 'right' }, { label: 'Running total', align: 'right' }]}
        rows={bars.map((b) => [b.label, formatValue(b.value), formatValue(b.to)])}
      />
    </figure>
  );
}
