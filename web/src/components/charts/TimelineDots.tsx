import { relativeAge } from './scale';

export interface TimelineRow {
  key: string;
  label: string;
  detail?: string;
  /** ISO time the thing was last seen, or null if it never was. */
  seenAt: string | null;
}

const DAY = 86_400_000;
const WINDOW_DAYS = 7;
/** Days back from now for each tick, placed where they fall in time (not evenly spaced). */
const TICKS: { days: number; label: string }[] = [
  { days: 7, label: '7 days ago' },
  { days: 5, label: '5 days' },
  { days: 3, label: '3 days' },
  { days: 1, label: '1 day' },
  { days: 0, label: 'now' },
];

/** When each thing was last seen, on one shared seven day axis. A hollow dot at the start means it is older or never seen. */
export function TimelineDots({ rows, now }: { rows: TimelineRow[]; now: number }) {
  return (
    <div className="@container">
      <div className="relative mb-2 hidden h-4 text-xs text-ink-soft @xl:block @xl:ml-[calc(11rem+1rem)] @xl:mr-[calc(7rem+1rem)]" aria-hidden="true">
        {TICKS.map((tick) => (
          <span
            key={tick.label}
            className="absolute top-0 whitespace-nowrap"
            style={{ left: `${(1 - tick.days / WINDOW_DAYS) * 100}%`, transform: tick.days === 0 ? 'translateX(-100%)' : tick.days === WINDOW_DAYS ? 'none' : 'translateX(-50%)' }}
          >
            {tick.label}
          </span>
        ))}
      </div>
      <ol className="flex flex-col gap-3">
        {rows.map((row) => {
          const age = row.seenAt ? now - new Date(row.seenAt).getTime() : null;
          const at = age === null ? 0 : Math.max(0, Math.min(1, 1 - age / (WINDOW_DAYS * DAY)));
          const faded = age === null || age > WINDOW_DAYS * DAY;
          return (
            <li key={row.key} className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-1 @xl:grid-cols-[minmax(0,11rem)_minmax(0,1fr)_7rem]">
              <span className="min-w-0 @xl:col-start-1 @xl:row-start-1">
                <span className="block truncate text-base font-medium" title={row.label}>
                  {row.label}
                </span>
                {row.detail && <span className="block truncate text-xs text-ink-soft">{row.detail}</span>}
              </span>
              <div className="relative col-span-2 row-start-2 h-6 @xl:col-span-1 @xl:col-start-2 @xl:row-start-1" aria-hidden="true">
                <div className="absolute inset-x-0 top-1/2 h-px -translate-y-1/2 bg-viz-grid" />
                <div
                  className="absolute top-1/2 size-3.5 -translate-x-1/2 -translate-y-1/2 rounded-full ring-2 ring-surface"
                  style={{ left: `${at * 100}%`, ...(faded ? { border: '2px solid var(--ink-soft)', background: 'var(--surface)' } : { background: 'var(--viz-accent)' }) }}
                />
              </div>
              <span className="text-right text-sm tabular-nums text-ink-soft @xl:col-start-3 @xl:row-start-1">{row.seenAt ? relativeAge(row.seenAt, now) : 'Never seen'}</span>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
