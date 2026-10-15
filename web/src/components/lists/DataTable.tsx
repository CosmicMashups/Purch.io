import type { KeyboardEvent, MouseEvent, ReactNode } from 'react';
import { CaretRight } from '@phosphor-icons/react';
import { SortHeader, useTableSort, type SortValue } from './tableSort';

const INTERACTIVE = 'button, a, input, select, textarea, label, [role="menu"], [role="menuitem"], [role="dialog"]';

export interface DataColumn<T> {
  /** Shown in the header and used as the column's identity, so keep it unique within a table. */
  header: string;
  cell: (row: T) => ReactNode;
  className?: string;
  /** Makes the column sortable by clicking its header: ascending, descending, then no sort. */
  sortValue?: (row: T) => SortValue;
}

/**
 * A plain table in the style of the Items and Ingredients lists, with click-to-sort column headers.
 * Loading, error and empty states stay with the page; give this the rows to show.
 */
export function DataTable<T>({
  rows,
  columns,
  getId,
  label,
  rowClassName,
  onRowClick,
  rowLabel,
}: {
  rows: T[];
  columns: DataColumn<T>[];
  getId: (row: T) => string;
  /** Names the table for assistive technology. */
  label?: string;
  rowClassName?: (row: T) => string | undefined;
  /** Makes the whole row open something. Controls inside the row (buttons, links, menus) keep their own click. */
  onRowClick?: (row: T) => void;
  /** Names the row's destination for assistive technology, for example "Open PR-00001". */
  rowLabel?: (row: T) => string;
}) {
  const sort = useTableSort(columns);
  const shown = sort.apply(rows);

  function rowProps(row: T) {
    if (!onRowClick) return {};
    return {
      tabIndex: 0,
      'aria-label': rowLabel?.(row),
      onClick: (e: MouseEvent<HTMLTableRowElement>) => {
        const target = e.target as HTMLElement;
        if (!e.currentTarget.contains(target) || target.closest(INTERACTIVE)) return;
        onRowClick(row);
      },
      onKeyDown: (e: KeyboardEvent<HTMLTableRowElement>) => {
        if (e.target !== e.currentTarget || (e.key !== 'Enter' && e.key !== ' ')) return;
        e.preventDefault();
        onRowClick(row);
      },
    };
  }

  return (
    <div className="overflow-x-auto overflow-y-hidden rounded-lg border border-gray-200 bg-white">
      <table className="min-w-full text-sm" aria-label={label}>
        <thead className="bg-gray-50 text-left text-xs font-medium uppercase text-gray-500">
          <tr>
            {columns.map((column) => (
              <SortHeader
                key={column.header}
                header={column.header}
                className={column.className}
                direction={sort.directionFor(column.header)}
                onToggle={column.sortValue ? () => sort.toggle(column.header) : undefined}
              />
            ))}
            {onRowClick && (
              <th scope="col" className="w-10 px-2 py-3">
                <span className="sr-only">Open</span>
              </th>
            )}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {shown.map((row) => (
            <tr
              key={getId(row)}
              {...rowProps(row)}
              className={`${onRowClick ? 'group cursor-pointer transition-colors duration-150 hover:bg-brand-tint/60 focus-visible:bg-brand-tint/60 focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand' : ''} ${rowClassName?.(row) ?? ''}`}
            >
              {columns.map((column) => (
                <td key={column.header} className={`px-4 py-3 ${column.className ?? ''}`}>
                  {column.cell(row)}
                </td>
              ))}
              {onRowClick && (
                <td className="w-10 px-2 py-3 text-right text-gray-300 transition-colors duration-150 group-hover:text-brand-strong">
                  <CaretRight size={18} aria-hidden="true" className="inline-block transition-transform duration-150 group-hover:translate-x-0.5" />
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
