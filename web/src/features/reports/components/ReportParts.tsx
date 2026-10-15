import type { ReactNode } from 'react';
import { toast } from '../../../components/feedback/toastStore';
import { csvRowCount, datedFilename, downloadTextFile } from '../../../lib/download';
import { businessToday } from '../range';
import type { RangeParams } from '../types';

/** One headline figure. `note` carries the caveat that stops the number being over-trusted. */
export function Stat({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="rounded-panel border border-line bg-surface p-4">
      <p className="text-sm text-ink-soft">{label}</p>
      <p className="mt-1 text-2xl font-bold tabular-nums tracking-tight">{value}</p>
      {note && <p className="mt-1 text-sm text-ink-soft">{note}</p>}
    </div>
  );
}

export interface Column<T> {
  header: string;
  cell: (row: T) => ReactNode;
  numeric?: boolean;
}

/** A plain, scrollable table for report rows. Numbers are right-aligned and tabular so columns line up. */
export function ReportTable<T>({ caption, columns, rows, rowKey }: { caption: string; columns: Column<T>[]; rows: T[]; rowKey: (row: T) => string }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[32rem] border-collapse text-left text-base">
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr className="border-b border-line text-sm text-ink-soft">
            {columns.map((c) => (
              <th key={c.header} scope="col" className={`px-3 py-2 font-semibold ${c.numeric ? 'text-right' : ''}`}>
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowKey(row)} className="border-b border-line/60">
              {columns.map((c) => (
                <td key={c.header} className={`px-3 py-3 align-top ${c.numeric ? 'text-right tabular-nums' : ''}`}>
                  {c.cell(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Segmented picker for sub-views that all come from the one response, so switching never refetches. */
export function SubViewTabs<T extends string>({ label, value, options, onChange }: { label: string; value: T; options: { id: T; label: string }[]; onChange: (id: T) => void }) {
  return (
    <div role="group" aria-label={label} className="inline-flex w-fit max-w-full flex-wrap overflow-hidden rounded-control border border-line print:hidden">
      {options.map((o) => (
        <button
          key={o.id}
          type="button"
          aria-pressed={value === o.id}
          onClick={() => onChange(o.id)}
          className={`h-12 px-4 text-base font-semibold ${value === o.id ? 'bg-ink text-canvas' : 'bg-surface hover:bg-canvas'}`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

function periodText(params: RangeParams): string {
  const fmt = (iso: string) => new Date(iso).toLocaleDateString('en-PH', { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'Asia/Manila' });
  return `${fmt(params.from)} to ${fmt(new Date(new Date(params.to).getTime() - 1).toISOString())}`;
}

/** Shown only when printing, so a saved PDF says what it covers. */
export function PrintHeader({ title, params, notes }: { title: string; params: RangeParams; notes?: string[] }) {
  return (
    <div className="mb-4 hidden print:block">
      <h1 className="text-2xl font-bold">{title}</h1>
      <p className="text-base">Period: {periodText(params)}</p>
      <p className="text-sm">Generated {new Date().toLocaleString('en-PH')}</p>
      {notes?.map((n) => (
        <p key={n} className="text-sm">
          {n}
        </p>
      ))}
    </div>
  );
}

interface ReportActionsProps {
  /** `profit-margin` becomes `profit-margin-2026-10-15.csv`. */
  filename: string;
  /** Built from the data on screen; return null to say there is nothing to export. */
  buildCsv: () => string | null;
  disabled?: boolean;
  /** The per-transaction listing, built on the server and Admin only. */
  listing?: { label: string; busy: boolean; onClick: () => void };
}

/** CSV comes from the cached figures; PDF is the browser's print dialog's "Save as PDF" over a print layout. */
export function ReportActions({ filename, buildCsv, disabled, listing }: ReportActionsProps) {
  function exportCsv() {
    const csv = buildCsv();
    if (!csv || csvRowCount(csv) === 0) return toast.info('There is nothing in this period to export.');
    downloadTextFile(datedFilename(filename, businessToday()), csv);
    toast.success(`Exported ${csvRowCount(csv)} row${csvRowCount(csv) === 1 ? '' : 's'}`);
  }

  const button = 'inline-flex h-12 items-center rounded-control border border-line bg-surface px-5 text-base font-semibold hover:border-brand disabled:opacity-50';
  return (
    <div className="flex flex-wrap gap-3 print:hidden">
      <button type="button" className={button} disabled={disabled} onClick={exportCsv}>
        Export CSV
      </button>
      <button type="button" className={button} disabled={disabled} onClick={() => window.print()}>
        Save as PDF
      </button>
      {listing && (
        <button type="button" className={button} disabled={disabled || listing.busy} onClick={listing.onClick}>
          {listing.busy ? 'Preparing...' : listing.label}
        </button>
      )}
    </div>
  );
}
