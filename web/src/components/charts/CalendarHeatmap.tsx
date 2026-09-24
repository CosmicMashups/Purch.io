import { ChartTip, TableView } from './ChartParts';
import { useChartTip } from './chartHooks';
import { calendarGrid, heatColor, heatLevel } from './scale';
import type { SeriesPoint } from './AreaLineChart';

const CELL = 34;
const GAP = 4;
const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const LABEL_W = 34;
const LEVELS = 5;

interface CalendarHeatmapProps {
  points: SeriesPoint[];
  seriesName: string;
  formatValue: (value: number) => string;
  ariaLabel: string;
}

/** A month at a glance: a column per week and a row per weekday, darker where more was sold. */
export function CalendarHeatmap({ points, seriesName, formatValue, ariaLabel }: CalendarHeatmapProps) {
  const { tip, show, hide } = useChartTip();
  const cells = calendarGrid(points.map((p) => p.key));
  const byKey = new Map(points.map((p) => [p.key, p]));
  const max = Math.max(...points.map((p) => p.value), 0);
  const cols = Math.max(0, ...cells.map((c) => c.col)) + 1;
  const width = LABEL_W + cols * (CELL + GAP);
  const height = 7 * (CELL + GAP);

  return (
    <figure>
      <div className="relative overflow-x-auto pb-1">
        <div className="relative" style={{ width, height }}>
          <svg width={width} height={height} role="img" aria-label={ariaLabel} className="block">
            {[1, 3, 5].map((row) => (
              <text key={row} x={0} y={row * (CELL + GAP) + CELL / 2 + 4} fontSize="12" fill="var(--ink-soft)">
                {WEEKDAYS[row]}
              </text>
            ))}
            {cells.map((cell) => {
              const point = byKey.get(cell.date)!;
              const level = heatLevel(point.value, max, LEVELS);
              const cx = LABEL_W + cell.col * (CELL + GAP);
              const cy = cell.row * (CELL + GAP);
              return (
                <rect
                  key={cell.date}
                  x={cx}
                  y={cy}
                  width={CELL}
                  height={CELL}
                  rx="6"
                  fill={heatColor(level)}
                  stroke={level === 0 ? 'var(--viz-grid)' : 'none'}
                  strokeWidth="1"
                  onPointerEnter={() => show(cx + CELL / 2, cy, { title: point.label, rows: [{ label: seriesName, value: formatValue(point.value), color: 'var(--viz-accent)' }] })}
                  onPointerLeave={hide}
                />
              );
            })}
          </svg>
          <ChartTip tip={tip} width={width} />
        </div>
      </div>

      <div className="mt-3 flex items-center gap-2 text-xs text-ink-soft" aria-hidden="true">
        <span>Less</span>
        {Array.from({ length: LEVELS + 1 }, (_, level) => (
          <span key={level} className="size-4 rounded-[4px]" style={{ background: heatColor(level), border: level === 0 ? '1px solid var(--viz-grid)' : 'none' }} />
        ))}
        <span>More</span>
      </div>

      <TableView caption={ariaLabel} columns={[{ label: 'Day' }, { label: seriesName, align: 'right' }]} rows={points.map((p) => [p.label, formatValue(p.value)])} />
    </figure>
  );
}
