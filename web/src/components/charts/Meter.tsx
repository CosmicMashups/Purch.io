import { CheckCircle, WarningCircle, XCircle } from '@phosphor-icons/react';

export type MeterTone = 'accent' | 'ok' | 'warn' | 'danger';

const FILL: Record<MeterTone, string> = {
  accent: 'var(--viz-accent)',
  ok: 'var(--color-ok)',
  warn: 'var(--color-warn)',
  danger: 'var(--color-danger)',
};

interface MeterProps {
  label: string;
  value: number;
  total: number;
  /** What is being counted, such as "items". */
  unit: string;
  tone?: MeterTone;
  /** Plain words for the state, shown with an icon so colour is never the only signal. */
  status?: string;
}

/** A count against its whole. The unfilled track is a pale step of the fill's own colour, so the state reads across the bar. */
export function Meter({ label, value, total, unit, tone = 'accent', status }: MeterProps) {
  const share = total > 0 ? Math.min(1, Math.max(0, value / total)) : 0;
  const Icon = tone === 'danger' ? XCircle : tone === 'warn' ? WarningCircle : CheckCircle;
  return (
    <div>
      <div className="flex items-baseline justify-between gap-4">
        <span className="text-base font-medium">{label}</span>
        <span className="text-sm tabular-nums text-ink-soft">
          <span className="font-semibold text-ink">{value}</span>
          {` of ${total} ${unit}`}
        </span>
      </div>
      <div
        role="meter"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={total}
        aria-valuenow={value}
        className="mt-2 h-2.5 overflow-hidden rounded-full"
        style={{ background: `color-mix(in srgb, ${FILL[tone]} 16%, var(--surface))` }}
      >
        <div className="h-full rounded-full" style={{ width: `${share * 100}%`, minWidth: value > 0 ? 6 : 0, background: FILL[tone] }} />
      </div>
      {status && (
        <p className="mt-1.5 flex items-center gap-1.5 text-sm font-medium" style={{ color: tone === 'accent' ? 'var(--ink-soft)' : FILL[tone] }}>
          <Icon size={16} weight="fill" aria-hidden="true" />
          {status}
        </p>
      )}
    </div>
  );
}
