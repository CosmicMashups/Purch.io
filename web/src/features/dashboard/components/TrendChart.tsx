import { formatDay, formatPeso, formatPesoCompact } from '../format';
import type { DailyRevenuePoint } from '../types';

const WIDTH = 720;
const HEIGHT = 220;
const PAD = { top: 12, right: 8, bottom: 28, left: 52 };

/** Daily revenue as bars, exactly as the server reports it. A screen-reader table carries the same numbers. */
export function TrendChart({ points }: { points: DailyRevenuePoint[] }) {
  const max = Math.max(...points.map((p) => p.revenue), 0);
  const plotW = WIDTH - PAD.left - PAD.right;
  const plotH = HEIGHT - PAD.top - PAD.bottom;
  const slot = plotW / points.length;
  const barW = Math.max(2, slot * 0.7);
  const ticks = max > 0 ? [0, max / 2, max] : [0];
  const labelAt = new Set([0, Math.floor((points.length - 1) / 2), points.length - 1]);

  return (
    <figure>
      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        role="img"
        aria-label={`Daily revenue, ${formatDay(points[0].date)} to ${formatDay(points[points.length - 1].date)}`}
        className="h-auto w-full"
      >
        {ticks.map((tick) => {
          const y = PAD.top + plotH - (max > 0 ? (tick / max) * plotH : 0);
          return (
            <g key={tick}>
              <line x1={PAD.left} x2={WIDTH - PAD.right} y1={y} y2={y} stroke="var(--line)" />
              <text x={PAD.left - 8} y={y + 4} textAnchor="end" fontSize="12" fill="var(--ink-soft)">
                {formatPesoCompact(tick)}
              </text>
            </g>
          );
        })}
        {points.map((point, i) => {
          const h = max > 0 ? (point.revenue / max) * plotH : 0;
          const x = PAD.left + i * slot + (slot - barW) / 2;
          return (
            <g key={point.date}>
              <rect x={x} y={PAD.top + plotH - h} width={barW} height={Math.max(h, point.revenue > 0 ? 2 : 0)} rx="2" fill="var(--brand)">
                <title>{`${formatDay(point.date)}: ${formatPeso(point.revenue)}`}</title>
              </rect>
              {labelAt.has(i) && (
                <text x={x + barW / 2} y={HEIGHT - 8} textAnchor="middle" fontSize="12" fill="var(--ink-soft)">
                  {formatDay(point.date)}
                </text>
              )}
            </g>
          );
        })}
      </svg>
      <table className="sr-only">
        <caption>Daily revenue</caption>
        <thead>
          <tr>
            <th>Date</th>
            <th>Revenue</th>
          </tr>
        </thead>
        <tbody>
          {points.map((p) => (
            <tr key={p.date}>
              <td>{formatDay(p.date)}</td>
              <td>{formatPeso(p.revenue)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
}
