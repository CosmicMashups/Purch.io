interface RankedRow {
  key: string;
  label: string;
  /** 0..1 share used for the bar length. */
  share: number;
  detail: string;
  tone?: 'brand' | 'warn' | 'danger';
}

const TONE: Record<NonNullable<RankedRow['tone']>, string> = {
  brand: 'bg-brand',
  warn: 'bg-warn',
  danger: 'bg-danger',
};

/** A labelled list where each row carries a proportional bar. The text always states the value too. */
export function RankedList({ rows }: { rows: RankedRow[] }) {
  return (
    <ol className="flex flex-col gap-4">
      {rows.map((row) => (
        <li key={row.key}>
          <div className="flex items-baseline justify-between gap-4">
            <span className="min-w-0 truncate text-base font-medium">{row.label}</span>
            <span className="shrink-0 text-sm tabular-nums text-ink-soft">{row.detail}</span>
          </div>
          <div className="mt-1 h-2 rounded-full bg-line" aria-hidden="true">
            <div className={`h-2 rounded-full ${TONE[row.tone ?? 'brand']}`} style={{ width: `${Math.round(row.share * 100)}%` }} />
          </div>
        </li>
      ))}
    </ol>
  );
}
