import { useState } from 'react';

export type SortValue = string | number | boolean | Date | null | undefined;
export type SortDirection = 'asc' | 'desc';
export interface SortState {
  column: string;
  direction: SortDirection;
}

/** The part of a column the sort logic needs. A column with no `sortValue` is not sortable. */
export interface SortableColumnLike<T> {
  header: string;
  sortValue?: (row: T) => SortValue;
}

function isEmpty(value: SortValue): boolean {
  return value === null || value === undefined || value === '';
}

function compareValues(a: NonNullable<SortValue>, b: NonNullable<SortValue>): number {
  if (a instanceof Date && b instanceof Date) return a.getTime() - b.getTime();
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  if (typeof a === 'boolean' && typeof b === 'boolean') return Number(a) - Number(b);
  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: 'base' });
}

/** Rows ordered by one column; with no sort the rows come back as given. Empty values always sort last. */
export function sortRows<T>(rows: T[], column: SortableColumnLike<T> | undefined, direction: SortDirection | null): T[] {
  if (!column?.sortValue || !direction) return rows;
  const get = column.sortValue;
  const sign = direction === 'asc' ? 1 : -1;
  return rows
    .map((row, index) => ({ row, index, value: get(row) }))
    .sort((x, y) => {
      const xEmpty = isEmpty(x.value);
      const yEmpty = isEmpty(y.value);
      if (xEmpty || yEmpty) return Number(xEmpty) - Number(yEmpty) || x.index - y.index;
      return sign * compareValues(x.value as NonNullable<SortValue>, y.value as NonNullable<SortValue>) || x.index - y.index;
    })
    .map((entry) => entry.row);
}

/** Sort state for a table: clicking a header goes ascending, then descending, then back to no sort. */
export function useTableSort<T>(columns: SortableColumnLike<T>[]) {
  const [sort, setSort] = useState<SortState | null>(null);
  const active = sort ? columns.find((c) => c.header === sort.column && c.sortValue) : undefined;
  const direction = active && sort ? sort.direction : null;

  function toggle(header: string) {
    setSort((current) => {
      if (!current || current.column !== header) return { column: header, direction: 'asc' };
      return current.direction === 'asc' ? { column: header, direction: 'desc' } : null;
    });
  }

  return {
    /** True while any column sort is on. */
    sorted: direction !== null,
    toggle,
    directionFor: (header: string): SortDirection | null => (direction && active?.header === header ? direction : null),
    apply: (rows: T[]) => sortRows(rows, active, direction),
  };
}

const ARIA_SORT = { asc: 'ascending', desc: 'descending' } as const;

/** A header cell that sorts its column when clicked. Without `onToggle` it is a plain header. */
export function SortHeader({
  header,
  direction,
  onToggle,
  className = '',
}: {
  header: string;
  direction: SortDirection | null;
  onToggle?: () => void;
  className?: string;
}) {
  const sortable = onToggle !== undefined && header !== '';
  return (
    <th scope="col" aria-sort={sortable ? (direction ? ARIA_SORT[direction] : 'none') : undefined} className={`px-4 py-3 ${className}`}>
      {sortable ? (
        <button
          type="button"
          onClick={onToggle}
          className={`inline-flex items-center gap-1 rounded uppercase hover:text-gray-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-brand ${direction ? 'text-gray-900' : ''}`}
        >
          {header}
          <svg viewBox="0 0 10 14" className="h-3 w-2.5" fill="currentColor" aria-hidden="true">
            <path d="M5 0 9 5H1z" className={direction === 'asc' ? '' : 'opacity-30'} />
            <path d="M5 14 1 9h8z" className={direction === 'desc' ? '' : 'opacity-30'} />
          </svg>
        </button>
      ) : header === '' ? (
        <span className="sr-only">Actions</span>
      ) : (
        header
      )}
    </th>
  );
}
