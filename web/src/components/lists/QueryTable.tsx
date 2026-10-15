import type { UseQueryResult } from '@tanstack/react-query';
import { ErrorState } from '../ErrorState';
import { SkeletonList } from '../Skeleton';
import { userMessage } from '../../lib/apiError';
import { DataTable, type DataColumn } from './DataTable';

/** A sortable table backed by a query, with the same loading, error and empty states as `QueryList`. */
export function QueryTable<T>({
  query,
  columns,
  getId,
  label,
  emptyMessage,
  errorTitle = 'Could not load this list',
  transform,
  rowClassName,
  onRowClick,
  rowLabel,
}: {
  query: UseQueryResult<T[]>;
  columns: DataColumn<T>[];
  getId: (row: T) => string;
  label?: string;
  emptyMessage: string;
  errorTitle?: string;
  /** Filters the rows for display without changing the cached data. */
  transform?: (rows: T[]) => T[];
  rowClassName?: (row: T) => string | undefined;
  onRowClick?: (row: T) => void;
  rowLabel?: (row: T) => string;
}) {
  if (query.isPending) return <SkeletonList />;
  if (query.isError) return <ErrorState title={errorTitle} message={userMessage(query.error)} onRetry={() => void query.refetch()} />;
  const rows = transform ? transform(query.data) : query.data;
  if (rows.length === 0) {
    return <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">{emptyMessage}</p>;
  }
  return <DataTable rows={rows} columns={columns} getId={getId} label={label} rowClassName={rowClassName} onRowClick={onRowClick} rowLabel={rowLabel} />;
}
