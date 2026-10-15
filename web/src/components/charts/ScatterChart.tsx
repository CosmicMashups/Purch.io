import { ChartTip, TableView } from './ChartParts';
import { useChartTip, useElementWidth } from './chartHooks';
import { median, quadrantOf, type Quadrant } from './chartData';
import { linear, niceTicks } from './scale';

export interface ScatterPoint {
  key: string;
  label: string;
  x: number;
  y: number;
}

interface ScatterChartProps {
  points: ScatterPoint[];
  xName: string;
  yName: string;
  formatX: (value: number) => string;
  formatY: (value: number) => string;
  ariaLabel: string;
  height?: number;
}

const PAD = { top: 20, right: 40, bottom: 48, left: 60 };

export const QUADRANT_LABEL: Record<Quadrant, string> = {
  star: 'Strong on both',
  volume: 'Sells a lot, earns little',
  niche: 'Earns well, sells little',
  review: 'Weak on both',
};

/** Each point against two measures, split at their medians, so the standouts and the laggards separate on sight. */
export function ScatterChart({ points, xName, yName, formatX, formatY, ariaLabel, height = 320 }: ScatterChartProps) {
  const [ref, width] = useElementWidth<HTMLDivElement>();
  const { tip, show, hide } = useChartTip();
  const xTicks = niceTicks(Math.max(...points.map((p) => p.x), 0));
  const yMax = Math.max(...points.map((p) => p.y), 0);
  const yTicks = niceTicks(yMax);
  const xTop = xTicks[xTicks.length - 1];
  const yTop = yTicks[yTicks.length - 1];
  const plotW = Math.max(60, width - PAD.left - PAD.right);
  const plotH = height - PAD.top - PAD.bottom;
  const x = linear([0, xTop], [PAD.left, PAD.left + plotW]);
  const y = linear([0, yTop], [PAD.top + plotH, PAD.top]);
  const mx = median(points.map((p) => p.x));
  const my = median(points.map((p) => p.y));

  return (
    <figure>
      <div ref={ref} className="relative">
        <svg width={width} height={height} role="img" aria-label={ariaLabel} className="block max-w-full">
          {yTicks.map((t) => (
            <g key={t}>
              <line x1={PAD.left} x2={width - PAD.right} y1={y(t)} y2={y(t)} stroke="var(--viz-grid)" strokeWidth="1" />
              <text x={PAD.left - 10} y={y(t) + 4} textAnchor="end" fontSize="12" fill="var(--ink-soft)">
                {formatY(t)}
              </text>
            </g>
          ))}
          {xTicks.map((t) => (
            <text key={t} x={x(t)} y={height - PAD.bottom + 18} textAnchor="middle" fontSize="12" fill="var(--ink-soft)">
              {formatX(t)}
            </text>
          ))}
          <text x={PAD.left + plotW / 2} y={height - 6} textAnchor="middle" fontSize="12" fontWeight="600" fill="var(--ink)">
            {xName}
          </text>
          <text x={14} y={PAD.top + plotH / 2} textAnchor="middle" fontSize="12" fontWeight="600" fill="var(--ink)" transform={`rotate(-90 14 ${PAD.top + plotH / 2})`}>
            {yName}
          </text>
          {points.length > 1 && (
            <g stroke="var(--ink-soft)" strokeDasharray="4 4" strokeOpacity="0.6">
              <line x1={x(mx)} x2={x(mx)} y1={PAD.top} y2={PAD.top + plotH} />
              <line x1={PAD.left} x2={PAD.left + plotW} y1={y(my)} y2={y(my)} />
            </g>
          )}
          {points.map((p) => {
            const q = quadrantOf(p.x, p.y, mx, my);
            const color = q === 'star' ? 'var(--viz-6)' : q === 'review' ? 'var(--viz-8)' : 'var(--viz-accent)';
            return (
              <g
                key={p.key}
                onPointerEnter={() =>
                  show(x(p.x), y(p.y), {
                    title: p.label,
                    rows: [
                      { label: xName, value: formatX(p.x) },
                      { label: yName, value: formatY(p.y) },
                      { label: QUADRANT_LABEL[q], value: '', color },
                    ],
                  })
                }
                onPointerLeave={hide}
              >
                <circle cx={x(p.x)} cy={y(p.y)} r="7" fill={color} fillOpacity="0.85" stroke="var(--surface)" strokeWidth="2" />
                <text x={x(p.x) > PAD.left + plotW * 0.72 ? x(p.x) - 11 : x(p.x) + 11} y={y(p.y) + 4} textAnchor={x(p.x) > PAD.left + plotW * 0.72 ? 'end' : 'start'} fontSize="12" fill="var(--ink)">
                  {p.label.length > 14 ? `${p.label.slice(0, 13)}...` : p.label}
                </text>
              </g>
            );
          })}
        </svg>
        <ChartTip tip={tip} width={width} />
      </div>
      <TableView
        caption={ariaLabel}
        columns={[{ label: 'Name' }, { label: xName, align: 'right' }, { label: yName, align: 'right' }, { label: 'Position' }]}
        rows={points.map((p) => [p.label, formatX(p.x), formatY(p.y), QUADRANT_LABEL[quadrantOf(p.x, p.y, mx, my)]])}
      />
    </figure>
  );
}
