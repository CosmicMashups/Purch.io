import { useState } from 'react';
import { ChartTip, TableView } from './ChartParts';
import { useChartTip, useElementWidth } from './chartHooks';
import { linear, niceTicks } from './scale';

export interface SeriesPoint {
  key: string;
  /** Text for the axis, tooltip and table, such as "Sep 24". */
  label: string;
  value: number;
}

interface AreaLineChartProps {
  points: SeriesPoint[];
  seriesName: string;
  formatValue: (value: number) => string;
  formatAxis: (value: number) => string;
  ariaLabel: string;
  height?: number;
}

const PAD = { top: 28, right: 20, bottom: 30, left: 56 };

/**
 * One series over time: a 2px line over a 10% wash, hairline grid, the best point labelled directly and the
 * latest point marked. A crosshair snaps to the nearest day and works with the arrow keys as well as the pointer.
 */
export function AreaLineChart({ points, seriesName, formatValue, formatAxis, ariaLabel, height = 248 }: AreaLineChartProps) {
  const [ref, width] = useElementWidth<HTMLDivElement>();
  const { tip, show, hide } = useChartTip();
  const [active, setActive] = useState<number | null>(null);

  const n = points.length;
  const max = Math.max(...points.map((p) => p.value), 0);
  const ticks = niceTicks(max);
  const top = ticks[ticks.length - 1];
  const plotW = Math.max(40, width - PAD.left - PAD.right);
  const plotH = height - PAD.top - PAD.bottom;
  const x = (i: number) => PAD.left + (n <= 1 ? plotW / 2 : (i / (n - 1)) * plotW);
  const y = linear([0, top], [PAD.top + plotH, PAD.top]);
  const baseline = PAD.top + plotH;

  const line = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${x(i).toFixed(1)} ${y(p.value).toFixed(1)}`).join(' ');
  const area = n > 1 ? `${line} L ${x(n - 1).toFixed(1)} ${baseline} L ${x(0).toFixed(1)} ${baseline} Z` : '';
  const bestIndex = points.reduce((best, p, i) => (p.value > points[best].value ? i : best), 0);
  const labelIndexes = [...new Set([0, Math.floor((n - 1) / 2), n - 1])];

  function focusPoint(i: number) {
    setActive(i);
    show(x(i), y(points[i].value), { title: points[i].label, rows: [{ label: seriesName, value: formatValue(points[i].value), color: 'var(--viz-accent)' }] });
  }

  function leave() {
    setActive(null);
    hide();
  }

  function onPointerMove(event: React.PointerEvent<SVGRectElement>) {
    const box = event.currentTarget.getBoundingClientRect();
    const rel = ((event.clientX - box.left) / Math.max(1, box.width)) * plotW;
    focusPoint(Math.min(n - 1, Math.max(0, Math.round((rel / plotW) * (n - 1)))));
  }

  function onKeyDown(event: React.KeyboardEvent<SVGSVGElement>) {
    const at = active ?? n - 1;
    const next = event.key === 'ArrowLeft' ? at - 1 : event.key === 'ArrowRight' ? at + 1 : event.key === 'Home' ? 0 : event.key === 'End' ? n - 1 : null;
    if (next === null) return;
    event.preventDefault();
    focusPoint(Math.min(n - 1, Math.max(0, next)));
  }

  return (
    <figure>
      <div ref={ref} className="relative">
        <svg
          width={width}
          height={height}
          role="group"
          aria-label={`${ariaLabel}. Use the left and right arrow keys to read each day.`}
          tabIndex={0}
          onKeyDown={onKeyDown}
          onBlur={leave}
          className="block max-w-full rounded-control"
        >
          {ticks.map((tick) => (
            <g key={tick}>
              <line x1={PAD.left} x2={width - PAD.right} y1={y(tick)} y2={y(tick)} stroke="var(--viz-grid)" strokeWidth="1" />
              <text x={PAD.left - 10} y={y(tick) + 4} textAnchor="end" fontSize="12" fill="var(--ink-soft)">
                {formatAxis(tick)}
              </text>
            </g>
          ))}

          {area && <path d={area} fill="var(--viz-accent)" fillOpacity="0.1" />}
          {n > 1 && <path d={line} fill="none" stroke="var(--viz-accent)" strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />}

          {labelIndexes.map((i) => (
            <text key={i} x={x(i)} y={height - 8} textAnchor={i === 0 && n > 1 ? 'start' : i === n - 1 && n > 1 ? 'end' : 'middle'} fontSize="12" fill="var(--ink-soft)">
              {points[i].label}
            </text>
          ))}

          {max > 0 && (
            <g>
              <circle cx={x(bestIndex)} cy={y(points[bestIndex].value)} r="4" fill="var(--viz-accent)" stroke="var(--surface)" strokeWidth="2" />
              <text
                x={Math.min(Math.max(x(bestIndex), PAD.left + 60), width - PAD.right - 60)}
                y={y(points[bestIndex].value) - 12}
                textAnchor="middle"
                fontSize="12"
                fontWeight="600"
                fill="var(--ink)"
              >
                {`Best: ${formatValue(points[bestIndex].value)}`}
              </text>
            </g>
          )}

          {n > 0 && bestIndex !== n - 1 && <circle cx={x(n - 1)} cy={y(points[n - 1].value)} r="4" fill="var(--viz-accent)" stroke="var(--surface)" strokeWidth="2" />}

          {active !== null && (
            <g pointerEvents="none">
              <line x1={x(active)} x2={x(active)} y1={PAD.top} y2={baseline} stroke="var(--ink-soft)" strokeOpacity="0.45" strokeWidth="1" />
              <circle cx={x(active)} cy={y(points[active].value)} r="5" fill="var(--viz-accent)" stroke="var(--surface)" strokeWidth="2" />
            </g>
          )}

          <rect x={PAD.left} y={PAD.top} width={plotW} height={plotH} fill="transparent" onPointerMove={onPointerMove} onPointerLeave={leave} />
        </svg>
        <ChartTip tip={tip} width={width} />
      </div>
      <TableView
        caption={ariaLabel}
        columns={[{ label: 'Day' }, { label: seriesName, align: 'right' }]}
        rows={points.map((p) => [p.label, formatValue(p.value)])}
      />
    </figure>
  );
}
