import type { ReactNode } from 'react';
import { useId, useState } from 'react';
import { CalendarBlank, CaretDown } from '@phosphor-icons/react';

interface PeriodChipProps {
  /** "Last 30 days" */
  label: string;
  /** "Sep 16 to Oct 15" */
  dates: string;
  /** A second line, such as the branch. */
  detail?: string;
  children: ReactNode;
}

/**
 * The reporting period as one outlined chip that opens its choices below it. The same control is used on the
 * Dashboard and in Reports so the period always looks and behaves the same, and never like a report button.
 */
export function PeriodChip({ label, dates, detail, children }: PeriodChipProps) {
  const [open, setOpen] = useState(false);
  const panelId = useId();

  return (
    <div className="flex flex-col gap-3">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((o) => !o)}
        className="flex min-h-14 w-full items-center gap-3 rounded-control border border-line bg-surface px-4 text-left hover:border-ink sm:w-auto sm:min-w-80"
      >
        <CalendarBlank size={24} aria-hidden="true" className="shrink-0 text-ink-soft" />
        <span className="flex flex-col">
          <span className="text-sm text-ink-soft">Period</span>
          <span className="text-base font-semibold">
            {label}
            <span className="font-normal text-ink-soft"> from {dates}</span>
          </span>
          {detail && <span className="text-sm text-ink-soft">{detail}</span>}
        </span>
        <CaretDown size={20} aria-hidden="true" className={`ml-auto shrink-0 text-ink-soft transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div id={panelId} className="flex flex-col gap-4 rounded-panel border border-line bg-surface p-4">
          {children}
        </div>
      )}
    </div>
  );
}
